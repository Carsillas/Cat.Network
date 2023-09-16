using System;
using System.Linq;
using System.Text;

namespace Cat.Network.Generator {
	public class ScopedStringWriter {

		private StringBuilder StringBuilder { get; } = new StringBuilder();

		private int Depth { get; set; }

		private void AppendDepth() {
			StringBuilder.Append(new string('\t', Depth));
		}
		
		public void Append(string text) {
			StringBuilder.Append(text);
		}

		public void AppendLine(string text = null) {
			StringBuilder.AppendLine();
			
			if (string.IsNullOrEmpty(text)) {
				return;
			}
			
			AppendDepth();
			StringBuilder.Append(text.Trim());
		}
		
		public void AppendBlock(string text) {
			if (string.IsNullOrEmpty(text)) {
				return;
			}

			string[] lines = text.Split('\n');

			int minDepth = lines.Min(CountLeadingTabs);
			
			for (var i = 0; i < lines.Length; i++) {
				var line = lines[i];

				if (i == 0 && (line == "\r\n" || line == "\n")) {
					continue;
				}
				
				AppendLine(line.Substring(minDepth).TrimEnd());
			}
		}

		private static int CountLeadingTabs(string line) {
			int count = 0;
			foreach (char c in line) {
				if (c == '\t') {
					count++;
				} else {
					break;
				}
			}

			return count;
		}
		
		public WriterScope EnterScope(string prefix = null) {

			if (string.IsNullOrWhiteSpace(prefix)) {
				AppendLine("{");
			} else {
				AppendLine(prefix);
				Append(" {");
			}
			
			
			Depth++;

			return new WriterScope(this);
		}
		
		public override string ToString() {
			return StringBuilder.ToString();
		}

		public struct WriterScope : IDisposable {
			public ScopedStringWriter Writer { get; }

			public WriterScope(ScopedStringWriter writer) {
				Writer = writer;
			}

			public void Dispose() {
				Writer.Depth--;
				Writer.AppendLine("}");
			}
		}
		
	}
	

}