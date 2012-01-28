// <copyright file="TestCoverageChecker.cs" company="Joerg Battermann">
//     (c) 2012 Joerg Battermann.
//     License: Microsoft Public License (Ms-PL). For details see https://github.com/jbattermann/JB.Tfs.Prototypes/blob/master/LICENSE
// </copyright>
// <author>Joerg Battermann</author>

using System;
using System.Collections.Generic;
using System.Linq;
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

            TfsTeamProjectCollection tfsTeamProjectCollection = TfsTeamProjectCollectionFactory.GetTeamProjectCollection(new Uri(tfsTeamProjectCollectionUri));

            if (tfsTeamProjectCollection == null)
                throw new ArgumentOutOfRangeException("tfsTeamProjectCollectionUri", "A TeamProjectCollection at the specified Uri does not exist");

            ITestManagementTeamProject testManagementTeamProject = tfsTeamProjectCollection.GetService<ITestManagementService>().GetTeamProject(projectName);

            if (testManagementTeamProject == null)
                throw new ArgumentOutOfRangeException("projectName", "A project with this name does not exist in this TeamProject Collection");

            var sourceWorkItem = testManagementTeamProject.WitProject.Store.GetWorkItem(workItemId);

            if (sourceWorkItem == null)
                throw new ArgumentOutOfRangeException("workItemId", "A Work Item with this Id does not exist in the given Project");

            // all set, now prepare rest of necessary data
            _requirementCategory =
                testManagementTeamProject.WitProject.Categories.FirstOrDefault(
                    currentCategory =>
                    currentCategory != null &&
                    currentCategory.ReferenceName.Equals("Microsoft.RequirementCategory",
                                                         StringComparison.InvariantCultureIgnoreCase));
            if (_requirementCategory == null)
                throw new ArgumentOutOfRangeException("projectName", "The given Project has no Requirement Category specified.");

            _testCaseCategory = 
                testManagementTeamProject.WitProject.Categories.FirstOrDefault(
                currentCategory =>
                    currentCategory != null &&
                    currentCategory.ReferenceName.Equals("Microsoft.TestCaseCategory",
                    StringComparison.InvariantCultureIgnoreCase));

            if (_testCaseCategory == null)
                throw new ArgumentOutOfRangeException("projectName", "The given Project has no TestCase Category specified.");

            _bugCategory =
                testManagementTeamProject.WitProject.Categories.FirstOrDefault(
                currentCategory =>
                    currentCategory != null &&
                    currentCategory.ReferenceName.Equals("Microsoft.BugCategory",
                    StringComparison.InvariantCultureIgnoreCase));

            if (_bugCategory == null)
                throw new ArgumentOutOfRangeException("projectName", "The given Project has no Bug Category specified.");

            // retrieved the categories successfully, now retrieve the corresponding work item types
            _bugWorkItemTypes = _bugCategory.WorkItemTypes.ToList();
            // bugs will get special treatment, hence the explicit exclusion from the requirements list below
            _requirementWorkItemTypes = _requirementCategory.WorkItemTypes.Except(_bugWorkItemTypes).ToList();
            _testCaseWorkItemTypes = _testCaseCategory.WorkItemTypes.ToList();

            // -------------------------------------------------------
            // alright, evaluation comes from now on

            // retrieve all active testplans
            var activeTestPlans = testManagementTeamProject.TestPlans.Query("Select * From TestPlan").Where(testPlan => testPlan.State == TestPlanState.Active).ToList();
            
        }

        [Empty, Help]
        public static void Help(string help)
        {
            Console.WriteLine(help);
        }
    }
}