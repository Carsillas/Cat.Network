using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading;

namespace Cat.Network.Generator {

	[Generator(LanguageNames.CSharp)]
	public class RPCGenerator : IIncrementalGenerator {

		public const string RPCAttributeMetadataName = "Cat.Network.Generator.RPCAttribute";
		public const string NetworkEntityMetadataName = "Cat.Network.Entities.NetworkEntity";

		private SymbolDisplayFormat FullyQualifiedFormat { get; } = new SymbolDisplayFormat(typeQualificationStyle: SymbolDisplayTypeQualificationStyle.NameAndContainingTypesAndNamespaces, genericsOptions: SymbolDisplayGenericsOptions.IncludeTypeParameters);
		private SymbolDisplayFormat TypeNameFormat { get; } = new SymbolDisplayFormat(typeQualificationStyle: SymbolDisplayTypeQualificationStyle.NameOnly, genericsOptions: SymbolDisplayGenericsOptions.IncludeTypeParameters);
		private SymbolDisplayFormat InterfaceMethodDeclarationFormat { get; } = new SymbolDisplayFormat(typeQualificationStyle: SymbolDisplayTypeQualificationStyle.NameAndContainingTypesAndNamespaces, parameterOptions: SymbolDisplayParameterOptions.IncludeType | SymbolDisplayParameterOptions.IncludeDefaultValue | SymbolDisplayParameterOptions.IncludeName, memberOptions: SymbolDisplayMemberOptions.IncludeParameters | SymbolDisplayMemberOptions.IncludeType);
		private SymbolDisplayFormat ClassMethodInvocationFormat { get; } = new SymbolDisplayFormat(typeQualificationStyle: SymbolDisplayTypeQualificationStyle.NameAndContainingTypesAndNamespaces, parameterOptions: SymbolDisplayParameterOptions.IncludeName, memberOptions: SymbolDisplayMemberOptions.IncludeParameters);

		public void Initialize(IncrementalGeneratorInitializationContext context) {

			IncrementalValuesProvider<RPCDeclaration> allClassesWithNetworkPropertyAttribute =
				context.SyntaxProvider.ForAttributeWithMetadataName(RPCAttributeMetadataName,
				(syntaxNode, cancellationToken) =>
				syntaxNode is MethodDeclarationSyntax &&
				syntaxNode.ChildNodes().Any(node => node is ExplicitInterfaceSpecifierSyntax),
				(generatorAttributeSyntaxContext, cancellationToken) => {

					IMethodSymbol symbol = (IMethodSymbol)generatorAttributeSyntaxContext.TargetSymbol;
					var node = (MethodDeclarationSyntax)generatorAttributeSyntaxContext.TargetNode;

					bool symbolIsNetworkEntity = false;
					INamedTypeSymbol containingTypeSymbol = symbol.ContainingType;
					INamedTypeSymbol currentClassSymbol = containingTypeSymbol;
					while (currentClassSymbol != null) {
						if (currentClassSymbol.ToDisplayString(FullyQualifiedFormat) == NetworkEntityMetadataName) {
							symbolIsNetworkEntity = true;
							break;
						}

						currentClassSymbol = currentClassSymbol.BaseType;
					}



					return new RPCDeclaration {
						Namespace = symbol.ContainingNamespace.ToDisplayString(FullyQualifiedFormat),
						ClassName = containingTypeSymbol.ToDisplayString(TypeNameFormat),
						ClassMetadataName = containingTypeSymbol.MetadataName,
						IsNetworkEntity = symbolIsNetworkEntity,
						MethodData = new MethodData {
							InterfaceMethodDeclaration = symbol.ToDisplayString(InterfaceMethodDeclarationFormat),
							ClassMethodInvocation = symbol.ToDisplayString(ClassMethodInvocationFormat)
						}
					};
				});

			IncrementalValuesProvider<RPCDeclaration> networkEntityRPCs =
				allClassesWithNetworkPropertyAttribute.Where(data => data.IsNetworkEntity);

			var collected = networkEntityRPCs.Collect();

			var classRPCDeclarations = collected.SelectMany((ImmutableArray<RPCDeclaration> collection, CancellationToken token) => {

				var grouped = collection.GroupBy(rpcDeclaration => $"{rpcDeclaration.Namespace}.{rpcDeclaration.ClassMetadataName}");

				return grouped.Select(grouping => new ClassRPCDeclarations {
					Namespace = grouping.First().Namespace,
					ClassName = grouping.First().ClassName,
					ClassMetadataName = grouping.First().ClassMetadataName,
					MethodDatas = grouping.Select(rpcDeclaration => rpcDeclaration.MethodData).ToList()
				});

			});

			context.RegisterSourceOutput(classRPCDeclarations,
				(c, source) => c.AddSource($"{source.Namespace}.{source.ClassMetadataName}.RPCs", source.GenerateSource()));
		}



		public struct RPCDeclaration {

			public string Namespace { get; set; }
			public string ClassName { get; set; }
			public string ClassMetadataName { get; set; }

			public bool IsNetworkEntity { get; set; }
			public MethodData MethodData { get; set; }
		}

		public struct MethodData {

			public string InterfaceMethodDeclaration { get; set; }
			public string ClassMethodInvocation { get; set; }

		}

		public struct ClassRPCDeclarations {

			public string Namespace { get; set; }
			public string ClassName { get; set; }
			public string ClassMetadataName { get; set; }
			public List<MethodData> MethodDatas { get; set; }

			public string GenerateSource() {

				return $@"

{GenerateClass()}
{GenerateInterface()}


";

			}


			private string GenerateInterface() {
				return $@"

namespace {Namespace}.RPC {{

	public interface {ClassName} {{
	
{GenerateInterfaceMethods()}

	}}

}}

";
			}


			private string GenerateClass() {
				return $@"

namespace {Namespace} {{

	partial class {ClassName} : RPC.{ClassName} {{

{GenerateClassMethods()}

	}}

}}

";
			}

			private string GenerateInterfaceMethods() {

				StringBuilder stringBuilder = new StringBuilder();

				foreach (MethodData method in MethodDatas) {
					stringBuilder.AppendLine($"\t\t{method.InterfaceMethodDeclaration};");
				}

				return stringBuilder.ToString();

			}

			private string GenerateClassMethods() {

				StringBuilder stringBuilder = new StringBuilder();

				// TODO DONT USE UNSAFE

				foreach (MethodData method in MethodDatas) {
					stringBuilder.AppendLine($@"
		public {method.InterfaceMethodDeclaration} {{
			System.Runtime.CompilerServices.Unsafe.As<RPC.{ClassName}>(this).{method.ClassMethodInvocation};
		}}
");
				}

				return stringBuilder.ToString();

			}

		}

	}



}
