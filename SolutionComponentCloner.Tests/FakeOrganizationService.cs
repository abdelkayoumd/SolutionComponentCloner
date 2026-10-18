using System;
using System.Collections.Generic;
using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Query;

namespace SolutionComponentCloner.Tests
{
    internal sealed class FakeOrganizationService : IOrganizationService
    {
        public List<OrganizationRequest> ExecutedRequests { get; } = new List<OrganizationRequest>();
        public List<QueryBase> Queries { get; } = new List<QueryBase>();
        public Func<OrganizationRequest, OrganizationResponse> OnExecute { get; set; } = r => new OrganizationResponse();
        public Func<QueryBase, EntityCollection> OnRetrieveMultiple { get; set; } = q => new EntityCollection();
        public List<Entity> Created { get; } = new List<Entity>();
        public Func<Entity, Guid> OnCreate { get; set; } = e => Guid.NewGuid();

        public OrganizationResponse Execute(OrganizationRequest request)
        {
            ExecutedRequests.Add(request);
            return OnExecute(request);
        }

        public EntityCollection RetrieveMultiple(QueryBase query)
        {
            Queries.Add(query);
            return OnRetrieveMultiple(query);
        }

        public Guid Create(Entity entity)
        {
            Created.Add(entity);
            return OnCreate(entity);
        }
        public Entity Retrieve(string entityName, Guid id, ColumnSet columnSet) => throw new NotSupportedException();
        public void Update(Entity entity) => throw new NotSupportedException();
        public void Delete(string entityName, Guid id) => throw new NotSupportedException();
        public void Associate(string entityName, Guid entityId, Relationship relationship, EntityReferenceCollection relatedEntities) => throw new NotSupportedException();
        public void Disassociate(string entityName, Guid entityId, Relationship relationship, EntityReferenceCollection relatedEntities) => throw new NotSupportedException();
    }
}
