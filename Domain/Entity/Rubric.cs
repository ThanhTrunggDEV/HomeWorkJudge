using System;
using Domain.ValueObject;

namespace Domain.Entity;

public class Rubric : EntityBase
{
    public RubricId Id { get; private set; }
    public AssignmentId AssignmentId { get; private set; }
    public string CriteriaListJson { get; private set; }

    public Rubric(RubricId id, AssignmentId assignmentId, string criteriaListJson)
    {
        if (string.IsNullOrWhiteSpace(criteriaListJson)) throw new ArgumentException("Criteria list JSON cannot be empty.");

        Id = id;
        AssignmentId = assignmentId;
        CriteriaListJson = criteriaListJson;
    }

    public void UpdateCriteria(string criteriaListJson)
    {
        if (string.IsNullOrWhiteSpace(criteriaListJson)) throw new ArgumentException("Criteria list JSON cannot be empty.");
        CriteriaListJson = criteriaListJson;
    }
}
