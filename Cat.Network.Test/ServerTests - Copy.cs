using Cat.Network.Entities;
using Microsoft.CodeAnalysis.CSharp;
using NUnit.Framework;
using System;

namespace Cat.Network.Test {
	public class ServerTest2 {

		[Test]
		public void Test_EntitySpawning() {

			var tree = CSharpSyntaxTree.ParseText(@"

		[Conditional(""RELEASE"")]
		void Test(){



		}
");


			Console.Write(tree);

		}


	}
}