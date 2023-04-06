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

using static Cat.Network.Generator.GeneratorUtils;

namespace Cat.Network.Generator {

	[Generator(LanguageNames.CSharp)]
	public class RPCGenerator : IIncrementalGenerator {

		public const string RPCPrefix = "RPC";
		public const string NetworkEntityMetadataName = "Cat.Network.Entities.NetworkEntity";

		private SymbolDisplayFormat FullyQualifiedFormat { get; } = new SymbolDisplayFormat(typeQualificationStyle: SymbolDisplayTypeQualificationStyle.NameAndContainingTypesAndNamespaces, genericsOptions: SymbolDisplayGenericsOptions.IncludeTypeParameters);
		private SymbolDisplayFormat TypeNameFormat { get; } = new SymbolDisplayFormat(typeQualificationStyle: SymbolDisplayTypeQualificationStyle.NameOnly, genericsOptions: SymbolDisplayGenericsOptions.IncludeTypeParameters);
		private SymbolDisplayFormat InterfaceMethodDeclarationFormat { get; } = new SymbolDisplayFormat(typeQualificationStyle: SymbolDisplayTypeQualificationStyle.NameAndContainingTypesAndNamespaces, parameterOptions: SymbolDisplayParameterOptions.IncludeType | SymbolDisplayParameterOptions.IncludeDefaultValue | SymbolDisplayParameterOptions.IncludeName, memberOptions: SymbolDisplayMemberOptions.IncludeParameters | SymbolDisplayMemberOptions.IncludeType);
		private SymbolDisplayFormat ClassMethodInvocationFormat { get; } = new SymbolDisplayFormat(typeQualificationStyle: SymbolDisplayTypeQualificationStyle.NameAndContainingTypesAndNamespaces, parameterOptions: SymbolDisplayParameterOptions.IncludeName, memberOptions: SymbolDisplayMemberOptions.IncludeParameters);

		public void Initialize(IncrementalGeneratorInitializationContext context) {

			IncrementalValuesProvider<RPCDeclaration> allMethodsWithRPCAttribute =
				context.SyntaxProvider.CreateSyntaxProvider(And(PassNodesOfType<MethodDeclarationSyntax>(), PassNodesWithExplicitInterfaceSpecifier(RPCPrefix)),
				(generatorSyntaxContext, cancellationToken) => {

					MethodDeclarationSyntax node = (MethodDeclarationSyntax)generatorSyntaxContext.Node;
					IMethodSymbol symbol = generatorSyntaxContext.SemanticModel.GetDeclaredSymbol(node, cancellationToken);

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
				allMethodsWithRPCAttribute.Where(data => data.IsNetworkEntity);

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
namespace {Namespace} {{

{GenerateClass()}

}}
";

			}


			private string GenerateInterface() {
				return $@"
		private interface {RPCPrefix} {{
{GenerateInterfaceMethods()}
		}}
";
			}


			private string GenerateClass() {
				return $@"
	partial class {ClassName} : {ClassName}.{RPCPrefix} {{
{GenerateInterface()}
{GenerateClassMethods()}
	}}
";
			}

			private string GenerateInterfaceMethods() {

				StringBuilder stringBuilder = new StringBuilder();

				foreach (MethodData method in MethodDatas) {
					stringBuilder.AppendLine($"\t\t\t{method.InterfaceMethodDeclaration};");
				}

				return stringBuilder.ToString();

			}

			private string GenerateClassMethods() {

				StringBuilder stringBuilder = new StringBuilder();

				foreach (MethodData method in MethodDatas) {
					stringBuilder.AppendLine($@"
		public {method.InterfaceMethodDeclaration} {{

			if (IsOwner) {{ 
				(({RPCPrefix})this).{method.ClassMethodInvocation};
			}} else {{
				// serialize and send
			}}
		}}
");
				}

				return stringBuilder.ToString();

			}

		}

	}



}
