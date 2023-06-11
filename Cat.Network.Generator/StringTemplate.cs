using System;
using System.Collections.Generic;
using System.Text;

namespace Cat.Network.Generator {
	internal class StringTemplate {

		public string Template { get; }
		public string[] Keys { get; }

		public StringTemplate(string template, params string[] keys) {
			Template = template;
			Keys = keys;
		}


		public string Apply(params string[] values) {
			if(Keys.Length != values.Length) {
				throw new Exception($"Cannot apply template with incorrect number of values. Keys: {string.Join(",", Keys)} Values: {string.Join(",", values)}");
			}

			string result = Template;
			for(int i = 0; i < Keys.Length; i++) {
				result = result.Replace($"{{{Keys[i]}}}", values[i]);
			}

			return result;
		}
	
	}
}
