using System.ComponentModel.DataAnnotations;

namespace StoryGenerator.Core.Models
{
    // Description Flow Models
    public class DescriptionFlowInput
    {
        public string? UserInput { get; set; }
        
        [Required]
        public string SessionId { get; set; } = string.Empty;
        
        public bool ClearSession { get; set; }
    }

    public class DescriptionFlowOutput
    {
        public string StoryPremise { get; set; } = string.Empty;
        public string NextQuestion { get; set; } = string.Empty;
        public List<string> PremiseOptions { get; set; } = new();
    }

    // Begin Story Flow Models
    public class BeginStoryFlowInput
    {
        [Required]
        public string UserInput { get; set; } = string.Empty;
        
        [Required]
        public string SessionId { get; set; } = string.Empty;
    }

    public class BeginStoryFlowOutput
    {
        public List<string> StoryParts { get; set; } = new();
        public List<string> Options { get; set; } = new();
        public string PrimaryObjective { get; set; } = string.Empty;
        public double Progress { get; set; }
    }

    // Continue Story Flow Models
    public class ContinueStoryFlowInput
    {
        [Required]
        public string UserInput { get; set; } = string.Empty;
        
        [Required]
        public string SessionId { get; set; } = string.Empty;
    }

    public class ContinueStoryFlowOutput
    {
        public List<string> StoryParts { get; set; } = new();
        public List<string> Options { get; set; } = new();
        public string PrimaryObjective { get; set; } = string.Empty;
        public double Progress { get; set; }
        public string Rating { get; set; } = string.Empty;
    }

    // Image Generation Models
    public class ImageGenerationInput
    {
        [Required]
        public string Story { get; set; } = string.Empty;
        
        [Required]
        public string SessionId { get; set; } = string.Empty;
    }

    public class StoryChoice
    {
        public string Choice { get; set; } = string.Empty;
        public string Rating { get; set; } = string.Empty;
    }

    // Internal models for OpenAI responses
    public class StoryDetailResponse
    {
        public string? Story { get; set; }
        public List<string> StoryParts { get; set; } = new();
        public string PrimaryObjective { get; set; } = string.Empty;
        public List<string> Milestones { get; set; } = new();
        public double Progress { get; set; }
        public List<StoryChoice> Choices { get; set; } = new();
    }

    public class ContinueStoryResponse
    {
        public string? Story { get; set; }
        public List<string> StoryParts { get; set; } = new();
        public string Rating { get; set; } = string.Empty;
        public string PrimaryObjective { get; set; } = string.Empty;
        public bool AchievedCurrentMilestone { get; set; }
        public double Progress { get; set; }
        public List<StoryChoice> Choices { get; set; } = new();
    }
}