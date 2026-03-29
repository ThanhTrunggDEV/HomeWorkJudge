using Domain.Entity;

namespace Domain.Policy;

public class LateSubmissionPolicy
{
    private readonly double _latePenaltyPercentage;

    public LateSubmissionPolicy(double latePenaltyPercentage)
    {
        if (latePenaltyPercentage < 0 || latePenaltyPercentage > 100)
        {
            throw new ArgumentOutOfRangeException(nameof(latePenaltyPercentage), "Late penalty percentage must be between 0 and 100.");
        }

        _latePenaltyPercentage = latePenaltyPercentage;
    }

    public double CalculatePenaltyPercentage(Assignment assignment, Submission submission)
    {
        if (!assignment.IsOverdue(submission.SubmitTime))
            return 0.0;

        return _latePenaltyPercentage;
    }
}

public class PlagiarismMatchPolicy
{
    private readonly double _plagiarismThreshold;

    public PlagiarismMatchPolicy(double plagiarismThreshold)
    {
        if (plagiarismThreshold < 0 || plagiarismThreshold > 100)
        {
            throw new ArgumentOutOfRangeException(nameof(plagiarismThreshold), "Plagiarism threshold must be between 0 and 100.");
        }

        _plagiarismThreshold = plagiarismThreshold;
    }

    public bool IsPlagiarized(double similarityScore)
    {
        return similarityScore > _plagiarismThreshold;
    }
}
