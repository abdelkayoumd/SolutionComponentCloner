using System;
using System.Linq;
using Microsoft.Crm.Sdk.Messages;
using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Query;
using SolutionComponentCloner.Models;
using SolutionComponentCloner.Services;
using Xunit;

namespace SolutionComponentCloner.Tests
{
    public class DataverseSolutionServiceTests
    {
        private readonly DataverseSolutionService _service = new DataverseSolutionService();

        private static SolutionComponentItem Component(int type) =>
            new SolutionComponentItem { ObjectId = Guid.NewGuid(), ComponentType = type };

        [Fact]
        public void CopyComponent_Entity_SetsDoNotIncludeSubcomponentsFromCheckbox()
        {
            var fake = new FakeOrganizationService();

            _service.CopyComponent(fake, Component(ComponentTypeCatalog.Entity), "target", addRequiredComponents: false);

            var request = Assert.IsType<AddSolutionComponentRequest>(fake.ExecutedRequests.Single());
            Assert.True(request.DoNotIncludeSubcomponents);
            Assert.False(request.AddRequiredComponents);
        }

        [Fact]
        public void CopyComponent_Entity_WithRequiredComponents_IncludesSubcomponents()
        {
            var fake = new FakeOrganizationService();

            _service.CopyComponent(fake, Component(ComponentTypeCatalog.Entity), "target", addRequiredComponents: true);

            var request = Assert.IsType<AddSolutionComponentRequest>(fake.ExecutedRequests.Single());
            Assert.False(request.DoNotIncludeSubcomponents);
            Assert.True(request.AddRequiredComponents);
        }

        [Fact]
        public void CopyComponent_NonEntity_NeverSetsDoNotIncludeSubcomponents()
        {
            var fake = new FakeOrganizationService();

            _service.CopyComponent(fake, Component(26), "target", addRequiredComponents: false);

            var request = Assert.IsType<AddSolutionComponentRequest>(fake.ExecutedRequests.Single());
            Assert.False(request.DoNotIncludeSubcomponents);
        }

        [Fact]
        public void CopyComponent_ReturnsFailureWithMessageWhenDataverseThrows()
        {
            var fake = new FakeOrganizationService
            {
                OnExecute = r => throw new InvalidOperationException("boom")
            };

            var result = _service.CopyComponent(fake, Component(26), "target", addRequiredComponents: true);

            Assert.Equal(CopyOutcome.Failed, result.Outcome);
            Assert.Equal("boom", result.Message);
        }

        [Fact]
        public void GetSolutions_PagesUntilNoMoreRecords()
        {
            var fake = new FakeOrganizationService();
            fake.OnRetrieveMultiple = q =>
            {
                var page = ((QueryExpression)q).PageInfo.PageNumber;
                var collection = new EntityCollection();
                collection.Entities.Add(NewSolution("Solution " + page));
                collection.MoreRecords = page < 3;
                collection.PagingCookie = "cookie-" + page;
                return collection;
            };

            var solutions = _service.GetSolutions(fake);

            Assert.Equal(3, solutions.Count);
            Assert.Equal(3, fake.Queries.Count);
            Assert.Equal("cookie-2", ((QueryExpression)fake.Queries[2]).PageInfo.PagingCookie);
        }

        private static Entity NewSolution(string name)
        {
            var entity = new Entity("solution", Guid.NewGuid());
            entity["friendlyname"] = name;
            entity["uniquename"] = name.Replace(" ", string.Empty);
            return entity;
        }
    }
}
