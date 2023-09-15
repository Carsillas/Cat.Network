using System;
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
			StringBuilder.AppendLine();

			if (string.IsNullOrEmpty(text)) {
				return;
			}

			string[] lines = text.Split('\n');

			foreach (string line in lines) {
				AppendLine(line.Trim());
			}
		}
		
		public WriterScope EnterScope(string prefix = null) {

			if (!string.IsNullOrEmpty(prefix)) {
				Append(" ");
			}
			
			AppendLine("{");
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