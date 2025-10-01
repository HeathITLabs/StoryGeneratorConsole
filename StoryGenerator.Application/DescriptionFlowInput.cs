namespace StoryGeneratorConsole.StoryGenerator.Application
{
    public class DescriptionFlowInput
    {
        public string SessionId { get; set; }
        public string? UserInput { get; set; }
        public bool ClearSession { get; set; }
    }
    public class DescriptionFlowOutput
    {
        public string StoryPremise { get; set; }
        public string NextQuestion { get; set; }
        public List<string> PremiseOptions { get; set; }
    }
}
