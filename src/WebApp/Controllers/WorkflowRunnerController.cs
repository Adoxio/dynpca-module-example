using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Xrm.Sdk;
using Microsoft.Crm.Sdk.Messages;
using System.Diagnostics;
using WebApp.Extensions;
using Microsoft.Xrm.Tooling.Connector;
using Microsoft.Xrm.Sdk.Client;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Authentication.JwtBearer;

namespace WebApp.Controllers
{
    public class WorkflowRunnerController : Controller
    {
        public CrmServiceClient CrmServiceClient;
        public OrganizationServiceContext ServiceContext;
        public IOrganizationService OrgService;

        public WorkflowRunnerController(CrmServiceClient crmClient, OrganizationServiceContext context, IOrganizationService orgSevice)
        {
            CrmServiceClient = crmClient;
            ServiceContext = context;
            OrgService = orgSevice;
        }       

        [Produces("application/json")]
        [Route("api/workflowrunner/create")]
        [HttpPost]
        public bool CreateRunner(string entityname, Guid workflowId, bool allowAnon = false)
        {
            if (string.IsNullOrEmpty(entityname) && workflowId == null) return false;

            var runner = new Entity("dynpca_workflowrunner");

            runner.Attributes["dynpca_allowanonymous"] = allowAnon;
            runner.Attributes["dynpca_workflow"] = new EntityReference("workflow", workflowId);
            runner.Attributes["dynpca_entitylogicalname"] = entityname;

            var runnerId = OrgService.Create(runner);

            if (runnerId != null)
                return true;
            return false;
        }

        [Produces("application/json")]
        [Route("api/workflowrunner/execute")]
        [HttpPost]
        public bool ExecuteWorkflow(Guid entityId, Guid runnerId)
        {

            var runner = ServiceContext.CreateQuery("dynpca_workflowrunner")
                .SingleOrDefault(a => a.GetAttributeValue<Guid>("dynpca_workflowrunnerid") == runnerId);

            if (runner == null)
                return false;

            // validate entity id record exists
            var entityLogicalName = runner.GetAttributeValue<string>("dynpca_entitylogicalname");
            var entityRecord = ServiceContext.CreateQuery(entityLogicalName).SingleOrDefault(a => a.Id == entityId);

            if (entityRecord == null)
                return false;

            var workflowId = runner.GetAttributeValue<EntityReference>("dynpca_workflow").Id;

            ExecuteWorkflowRequest request;
            ExecuteWorkflowResponse response;

            if (runner.GetAttributeValue<bool>("dynpca_allowanonymous"))
            {
                request = new ExecuteWorkflowRequest()
                {
                    WorkflowId = workflowId,
                    EntityId = entityId
                };

                response = (ExecuteWorkflowResponse)OrgService.Execute(request);
                return true;
            }


            var identity = (ClaimsIdentity)User.Identity;

            if (!runner.GetAttributeValue<bool>("dynpca_allowanonymous") && !identity.IsAuthenticated)
            {
                var contact = OrgService.GetContact(identity);

                return false;
            }
            
            return false;
        }
    }
}