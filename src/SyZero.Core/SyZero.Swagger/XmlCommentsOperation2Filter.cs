
using Microsoft.AspNetCore.Mvc.ApiExplorer;
using Microsoft.OpenApi.Models;
using Swashbuckle.AspNetCore.SwaggerGen;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Xml.XPath;

namespace SyZero.Swagger
{
    public class XmlCommentsOperation2Filter : IOperationFilter
    {
        private const string MemberXPath = "/doc/members/member[@name='{0}']";
        private const string SummaryXPath = "summary";
        private const string RemarksXPath = "remarks";
        private const string ParamXPath = "param[@name='{0}']";
        private const string ResponsesXPath = "response";

        private readonly XPathNavigator _xmlNavigator;

        public XmlCommentsOperation2Filter(XPathDocument xmlDoc)
        {
            _xmlNavigator = xmlDoc.CreateNavigator();
        }

        private MethodInfo GetGenericTypeMethodOrNullFor(MethodInfo constructedTypeMethod)
        {
            var constructedType = constructedTypeMethod.DeclaringType;
            if (constructedType == null || !constructedType.IsConstructedGenericType)
            {
                return null;
            }

            var genericTypeDefinition = constructedType.GetGenericTypeDefinition();

            var metadataMatchedMethod = genericTypeDefinition.GetMethods()
                .FirstOrDefault(method => method.MetadataToken == constructedTypeMethod.MetadataToken);
            if (metadataMatchedMethod != null)
            {
                return metadataMatchedMethod;
            }

            var candidateMethods = genericTypeDefinition.GetMethods()
                .Where(method => method.Name == constructedTypeMethod.Name)
                .Where(method => ParametersMatch(method.GetParameters(), constructedTypeMethod.GetParameters()))
                .ToArray();

            return candidateMethods.Length == 1 ? candidateMethods[0] : null;
        }

        private static bool ParametersMatch(ParameterInfo[] candidateParameters, ParameterInfo[] constructedParameters)
        {
            if (candidateParameters.Length != constructedParameters.Length)
            {
                return false;
            }

            for (var i = 0; i < candidateParameters.Length; i++)
            {
                if (!string.Equals(
                    XmlCommentsMemberNameHelper.GetQualifiedNameForXml(candidateParameters[i].ParameterType, expandGenericArgs: true),
                    XmlCommentsMemberNameHelper.GetQualifiedNameForXml(constructedParameters[i].ParameterType, expandGenericArgs: true),
                    StringComparison.Ordinal))
                {
                    return false;
                }
            }

            return true;
        }

        private void ApplyMethodXmlToOperation(OpenApiOperation operation, XPathNavigator methodNode)
        {
            var summaryNode = methodNode.SelectSingleNode(SummaryXPath);
            if (summaryNode != null)
                operation.Summary = XmlCommentsTextHelper.Humanize(summaryNode.InnerXml);

            var remarksNode = methodNode.SelectSingleNode(RemarksXPath);
            if (remarksNode != null)
                operation.Description = XmlCommentsTextHelper.Humanize(remarksNode.InnerXml);
        }

        private void ApplyParamsXmlToActionParameters(
       IList<OpenApiParameter> parameters,
       XPathNavigator methodNode,
       ApiDescription apiDescription)
        {
            if (parameters == null) return;

            foreach (var parameter in parameters)
            {
                // Check for a corresponding action parameter?
                var actionParameter = apiDescription.ActionDescriptor.Parameters
                    .FirstOrDefault(p => parameter.Name.Equals(
                        (p.BindingInfo?.BinderModelName ?? p.Name), StringComparison.OrdinalIgnoreCase));
                if (actionParameter == null) continue;

                var paramNode = methodNode.SelectSingleNode(string.Format(ParamXPath, actionParameter.Name));
                if (paramNode != null)
                    parameter.Description = XmlCommentsTextHelper.Humanize(paramNode.InnerXml);
            }
        }
        private void ApplyResponsesXmlToResponses(IDictionary<string, OpenApiResponse> responses, XPathNodeIterator responseNodes)
        {
            while (responseNodes.MoveNext())
            {
                var code = responseNodes.Current.GetAttribute("code", "");
                var response = responses.ContainsKey(code)
                    ? responses[code]
                    : responses[code] = new OpenApiResponse();

                response.Description = XmlCommentsTextHelper.Humanize(responseNodes.Current.InnerXml);
            }
        }

        public void Apply(OpenApiOperation operation, OperationFilterContext context)
        {
            if (context.MethodInfo == null) return;

            // If method is from a constructed generic type, look for comments from the generic type method
            var targetMethod = context.MethodInfo.DeclaringType?.IsConstructedGenericType == true
                ? GetGenericTypeMethodOrNullFor(context.MethodInfo)
                : context.MethodInfo;

            if (targetMethod == null) return;

           var memberName = XmlCommentsMemberNameHelper.GetMemberNameForMethod(targetMethod);
            var methodNode = _xmlNavigator.SelectSingleNode(string.Format(MemberXPath, memberName));

            if (methodNode == null) {
                 memberName = XmlCommentsMemberNameHelper.GetMemberNameForInterfaceMethod(targetMethod);
                 methodNode = _xmlNavigator.SelectSingleNode(string.Format(MemberXPath, memberName));
            }
            if (methodNode != null)
            {
                ApplyMethodXmlToOperation(operation, methodNode);
                ApplyParamsXmlToActionParameters(operation.Parameters, methodNode, context.ApiDescription);
                ApplyResponsesXmlToResponses(operation.Responses, methodNode.Select(ResponsesXPath));
            }
           

     
        }
    }


}
