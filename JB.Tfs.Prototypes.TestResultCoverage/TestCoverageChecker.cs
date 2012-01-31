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

        private static IList<ITestCase> _testCases; 
        private static IList<ITestConfiguration> _testConfigurations;

        private const string CategoryRequirement = "Microsoft.RequirementCategory";
        private const string CategoryTestCase = "Microsoft.TestCaseCategory";
        private const string CategoryBug = "Microsoft.BugCategory";
        private const string TestedByLinkTypeReferenceName = "Microsoft.VSTS.Common.TestedBy";

        [Verb(IsDefault = true)]
        public static void CheckTestCoverage(
            [Parameter(Aliases = "tpc", Description = "The TeamProjectCollection Uri (e.g. 'http://tfsserver.local:8080/tfs/DefaultCollection') the workitem to check for is contained in")]
                string tfsTeamProjectCollectionUri,
            [Parameter(Aliases = "p", Description = "The Name of the Project (e.g. 'SomeProject') of the Project the workitem to check is contained in")]
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

            _testConfigurations = _testManagementTeamProject.TestConfigurations.Query(
                "Select * from TestConfiguration").ToList();

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

            _testCases = (_testManagementTeamProject.CreateTestQuery(testCaseIds.ToArray())).Execute().ToList();

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


            var testPointsInTestPlans = activeTestPlans.ToDictionary(testPlan => testPlan, testPlan => testPlan.QueryTestPoints(queryStringBuilder.ToString()).ToList());

            // ToDo: Check for configurations and their individual runs
            // ToDo: Check all, not only the most recent results. This shall be an option, as some businesses require this

            var testCasePerConfigurationsTestOutcomes = new Dictionary<ITestCase, Dictionary<ITestPlan, Dictionary<ITestConfiguration, TestOutcome>>>();

            foreach (var testCase in _testCases)
            {
                var testCaseId = testCase.Id;
                var perTestplanDictionary = testPointsInTestPlans.Where(
                        valuePair => valuePair.Value.Any(testPoint => testPoint.TestCaseId == testCaseId)).ToDictionary(
                        outterKeyValuePair => outterKeyValuePair.Key, outterKeyValuePair => outterKeyValuePair.Value.ToDictionary(
                            innerKeyValuePair =>
                                _testConfigurations.First(testConfiguration => testConfiguration.Id == innerKeyValuePair.ConfigurationId),
                                innerKeyValuePair => innerKeyValuePair.MostRecentResultOutcome));

                if (perTestplanDictionary.Count > 0)
                    testCasePerConfigurationsTestOutcomes.Add(testCase, perTestplanDictionary);
            }

            Console.WriteLine("Done.");
            Console.WriteLine("-------------------------------------------------");
            Console.WriteLine("Starting point was the WorkItem '{0}' (Id: {1}) which had {2} Test Case(s) linked to it. The Evaluation of these is the following:", _sourceWorkItem.Title, _sourceWorkItem.Id, _testCases.Count);
            foreach (var testCasePerConfigurationsTestOutcome in testCasePerConfigurationsTestOutcomes)
            {
                bool failed = false;
                bool inconclusive = false;
                Console.WriteLine();
                Console.WriteLine("TestCase '{0}' (Id: {1}) is used in {2} active TestPlans.", testCasePerConfigurationsTestOutcome.Key.Title, testCasePerConfigurationsTestOutcome.Key.Id, testCasePerConfigurationsTestOutcome.Value.Count);
                foreach (var casePerConfigurationsTestOutcome in testCasePerConfigurationsTestOutcome.Value)
                {
                    Console.WriteLine();
                    Console.WriteLine("In Testplan '{0}' it was planned with {1} Test Configuration(s). Test Outcomes per Configuration:", casePerConfigurationsTestOutcome.Key.Name, casePerConfigurationsTestOutcome.Value.Count);
                    foreach (var outcome in casePerConfigurationsTestOutcome.Value)
                    {
                        Console.WriteLine("- '{0}' (default: {1}): {2}", outcome.Key.Name, outcome.Key.IsDefault ? "yes" : "no", outcome.Value);
                        switch (outcome.Value)
                        {
                            case TestOutcome.Warning:
                            case TestOutcome.Inconclusive:
                            case TestOutcome.NotExecuted:
                            case TestOutcome.None:
                                inconclusive = true;
                                break;
                            case TestOutcome.Aborted:
                            case TestOutcome.Failed:
                            case TestOutcome.Error:
                            case TestOutcome.Blocked:
                            case TestOutcome.Timeout:
                                failed = true;
                                break;
                            case TestOutcome.Passed:
                                break;
                        }
                    }
                }

                string finalResult;
                ConsoleColor consoleColor;
                if (failed)
                {
                    finalResult = "Failed";
                    consoleColor = ConsoleColor.Red;
                }
                else if (inconclusive)
                {
                    finalResult = "Inconclusive";
                    consoleColor = ConsoleColor.Yellow;
                }
                else
                {
                    finalResult = "Passed";
                    consoleColor = ConsoleColor.Green;
                }
                Console.WriteLine();
                Console.Write("Therefore the overall Test Coverage and Test Results for Workitem '{0}' is: ", _sourceWorkItem.Title);
                var oldColor = Console.ForegroundColor;
                Console.ForegroundColor = consoleColor;
                Console.Write(finalResult.ToUpper());
                Console.ForegroundColor = oldColor;
            }
        }

        [Empty, Help]
        public static void Help(string help)
        {
            Console.WriteLine(help);
        }
    }
}