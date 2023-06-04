namespace Cat.Network.Generator {
	public struct NetworkPropertyData {
		public bool Declared { get; set; }
		public string Name { get; set; }
		public byte AccessModifier { get; set; }
		public string AccessModifierText {
			get {
				switch (AccessModifier) {
					case 0: return "public";
					case 1: return "protected";
					case 2: return "private";
				}
				return "";
			}
		}

		public string FullyQualifiedTypeName { get; set; }

		public string InterfacePropertyDeclaration => $"{AccessModifierText} {FullyQualifiedTypeName} {Name} {{ get; set; }}";

	}
}
