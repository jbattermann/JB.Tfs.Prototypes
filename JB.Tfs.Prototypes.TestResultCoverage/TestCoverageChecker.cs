// <copyright file="TestCoverageChecker.cs" company="Joerg Battermann">
//     (c) 2012 Joerg Battermann.
//     License: see https://github.com/jbattermann/JB.Tfs.Prototypes/blob/master/LICENSE
// </copyright>
// <author>Joerg Battermann</author>

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using CLAP;
using CLAP.Validation;
using Microsoft.TeamFoundation.Client;
using Microsoft.TeamFoundation.TestManagement.Client;
using Microsoft.TeamFoundation.WorkItemTracking.Client;

namespace JB.Tfs.Prototypes.TestResultCoverage
{
    class TestCoverageChecker
    {
        private static Category _requirementCategory;
        private static Category _testCaseCategory;
        private static Category _bugCategory;

        private static IList<WorkItemType> _requirementWorkItemTypes;
        private static IList<WorkItemType> _testCaseWorkItemTypes;
        private static IList<WorkItemType> _bugWorkItemTypes;

        private static TfsTeamProjectCollection _tfsTeamProjectCollection;
        private static ITestManagementTeamProject _testManagementTeamProject;
        private static WorkItem _sourceWorkItem;
        private static WorkItemLinkType _testedByworkItemLinkType;

        private const string CategoryRequirement = "Microsoft.RequirementCategory";
        private const string CategoryTestCase = "Microsoft.TestCaseCategory";
        private const string CategoryBug = "Microsoft.BugCategory";
        private const string TestedByLinkTypeReferenceName = "Microsoft.VSTS.Common.TestedBy";

        [Verb(IsDefault = true)]
        public static void CheckTestCoverage(
            [Parameter(Aliases = "tpc", Description = "The TeamProjectCollection Uri (e.g. 'http://tfsserver.local:8080/tfs/DefaultCollection') the workitem to check for is contained in")]
                string tfsTeamProjectCollectionUri,
            [Parameter(Aliases = "p", Description = "The Name of the Project (e.g. 'http://tfsserver.local:8080/tfs/DefaultCollection') of the TFS TeamProjectCollection the workitem to check is contained in")]
                string projectName,
            [MoreThan(0)][Parameter(Aliases = "wi", Description = "The WorkItem Id to check TestCoverage for")]
                int workItemId)
        {
            if (tfsTeamProjectCollectionUri == null) throw new ArgumentNullException("tfsTeamProjectCollectionUri");
            if (projectName == null) throw new ArgumentNullException("projectName");

            if (string.IsNullOrWhiteSpace(tfsTeamProjectCollectionUri))
                throw new ArgumentOutOfRangeException("tfsTeamProjectCollectionUri", "cannot be empty");
            if (string.IsNullOrWhiteSpace(projectName))
                throw new ArgumentOutOfRangeException("projectName", "cannot be empty");

            _tfsTeamProjectCollection = TfsTeamProjectCollectionFactory.GetTeamProjectCollection(new Uri(tfsTeamProjectCollectionUri));

            if (_tfsTeamProjectCollection == null)
                throw new ArgumentOutOfRangeException("tfsTeamProjectCollectionUri", "A TeamProjectCollection at the specified Uri does not exist");

            _testManagementTeamProject = _tfsTeamProjectCollection.GetService<ITestManagementService>().GetTeamProject(projectName);

            if (_testManagementTeamProject == null)
                throw new ArgumentOutOfRangeException("projectName", "A project with this name does not exist in this TeamProject Collection");

            _sourceWorkItem = _testManagementTeamProject.WitProject.Store.GetWorkItem(workItemId);

            if (_sourceWorkItem == null)
                throw new ArgumentOutOfRangeException("workItemId", "A Work Item with this Id does not exist in the given Project");

            // all set, now prepare rest of necessary data
            _requirementCategory =
                _testManagementTeamProject.WitProject.Categories.FirstOrDefault(
                    currentCategory =>
                    currentCategory != null &&
                    currentCategory.ReferenceName.Equals(CategoryRequirement,
                                                         StringComparison.InvariantCultureIgnoreCase));
            if (_requirementCategory == null)
                throw new ArgumentOutOfRangeException("projectName",
                    "The given Project has no Requirement Category specified. See http://msdn.microsoft.com/en-us/library/dd286631.aspx for details.");

            _testCaseCategory = 
                _testManagementTeamProject.WitProject.Categories.FirstOrDefault(
                currentCategory =>
                    currentCategory != null &&
                    currentCategory.ReferenceName.Equals(CategoryTestCase,
                    StringComparison.InvariantCultureIgnoreCase));

            if (_testCaseCategory == null)
                throw new ArgumentOutOfRangeException("projectName",
                    "The given Project has no TestCase Category specified. See http://msdn.microsoft.com/en-us/library/dd286631.aspx for details.");

            _bugCategory =
                _testManagementTeamProject.WitProject.Categories.FirstOrDefault(
                currentCategory =>
                    currentCategory != null &&
                    currentCategory.ReferenceName.Equals(CategoryBug,
                    StringComparison.InvariantCultureIgnoreCase));

            if (_bugCategory == null)
                throw new ArgumentOutOfRangeException("projectName",
                    "The given Project has no Bug Category specified. See http://msdn.microsoft.com/en-us/library/dd286631.aspx for details.");

            // retrieving TestedBy link type
            _testedByworkItemLinkType =
                _testManagementTeamProject.WitProject.Store.WorkItemLinkTypes.FirstOrDefault(
                workItemLinkType =>
                    workItemLinkType.ReferenceName.Equals(TestedByLinkTypeReferenceName,
                    StringComparison.InvariantCultureIgnoreCase));

            if (_testedByworkItemLinkType == null)
                throw new ArgumentOutOfRangeException("tfsTeamProjectCollectionUri", "The given ProjectCollection has no TestedBy LinkType Defined. See http://msdn.microsoft.com/en-us/library/dd293527.aspx for details.");

            // retrieved the categories successfully, now retrieve the corresponding work item types
            _bugWorkItemTypes = _bugCategory.WorkItemTypes.ToList();
            // bugs will get special treatment, hence the explicit exclusion from the requirements list below
            _requirementWorkItemTypes = _requirementCategory.WorkItemTypes.Except(_bugWorkItemTypes).ToList();
            _testCaseWorkItemTypes = _testCaseCategory.WorkItemTypes.ToList();

            // retrieve all linked test cases to the source / starting work item
            var testCaseIds = new List<int>();
            foreach (var workItemLink in
                from WorkItemLink workItemLink in _sourceWorkItem.WorkItemLinks
                where workItemLink.LinkTypeEnd == _testedByworkItemLinkType.ForwardEnd
                where !testCaseIds.Contains(workItemLink.TargetId)
                select workItemLink)
            {
                testCaseIds.Add(workItemLink.TargetId);
            }

            if (testCaseIds.Count == 0)
                throw new ArgumentOutOfRangeException("workItemId", string.Format("No TestCases are linked to the given Requirement with the id '{0}'.", workItemId));

            #region not pursuing this option for now
            //var abc = _testManagementTeamProject.TestResults.ByTestId(workItemId);

            //foreach (var testCaseResult in abc)
            //{
            //    Console.WriteLine(testCaseResult.Outcome);
            //}
            #endregion

            // retrieve all active testplans
            // Todo: make 'only active ones' an option
            var activeTestPlans = _testManagementTeamProject.TestPlans.Query("Select * From TestPlan").Where(testPlan => testPlan.State == TestPlanState.Active).ToList();

            if (activeTestPlans.Count == 0)
                throw new ArgumentOutOfRangeException("projectName", "There are no active TestPlans in the given Project.");
            
            // build up query string for the test case ids found that are linked to the item
            var queryStringBuilder = new StringBuilder("SELECT * FROM TestPoint");
            var isPastFirstTestCaseId = false;

            foreach (var testCaseId in testCaseIds)
            {
                if (isPastFirstTestCaseId)
                    queryStringBuilder.Append(string.Format(" or TestCaseId = {0}", testCaseId));
                else
                {
                    queryStringBuilder.Append(string.Format(" Where TestCaseId = {0}", testCaseId));
                    isPastFirstTestCaseId = true;
                }
            }

            var testPoints = new List<ITestPoint>();
            foreach (var testPlan in activeTestPlans)
            {
                testPoints.AddRange(testPlan.QueryTestPoints(queryStringBuilder.ToString()));
            }

            // ToDo: Check for configurations and their individual runs
            // ToDo: Check all, not only the most recent results. This shall be an option, as some businesses require this
            var notExecutedTestPoints = new List<ITestPoint>();
            var failedTestPoints = new List<ITestPoint>();
            var inconclusiveTestPoints = new List<ITestPoint>();
            var warningTestPoints = new List<ITestPoint>();
            var passedTestPoints = new List<ITestPoint>();

            // put test points into their corresponding bins for final result evaluation
            foreach (var testPoint in testPoints)
            {
                // check whether a test run has actual taken place, yet (or if it was just planned for now)
                if (testPoint.MostRecentResult == null ||
                    testPoint.MostRecentResult.Outcome == TestOutcome.NotExecuted ||
                    testPoint.MostRecentResult.Outcome == TestOutcome.None)
                {
                    notExecutedTestPoints.Add(testPoint);
                    continue;
                }

                // check outcome
                switch (testPoint.MostRecentResult.Outcome)
                {
                    case TestOutcome.Inconclusive:
                    // case TestOutcome.Unspecified: // this is only a temporary enum value set during value changes
                        inconclusiveTestPoints.Add(testPoint);
                        break;
                    case TestOutcome.Aborted:
                    case TestOutcome.Failed:
                    case TestOutcome.Error:
                    case TestOutcome.Blocked:
                    case TestOutcome.Timeout:
                        failedTestPoints.Add(testPoint);
                        break;
                    case TestOutcome.Warning:
                        warningTestPoints.Add(testPoint);
                        break;
                    case TestOutcome.Passed:
                        passedTestPoints.Add(testPoint);
                        break;
                }

                // do final round
            }
        }

        [Empty, Help]
        public static void Help(string help)
        {
            Console.WriteLine(help);
        }
    }
}