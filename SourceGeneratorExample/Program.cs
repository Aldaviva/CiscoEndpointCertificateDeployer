namespace SourceGeneratorExample {

    // ReSharper disable once PartialTypeWithSinglePart
    public partial class Program {

        private static void Main(string[] args) {
            HelloFrom("Generated Code");
        }

        // ReSharper disable once PartialMethodWithSinglePart
        static partial void HelloFrom(string name);

    }

}