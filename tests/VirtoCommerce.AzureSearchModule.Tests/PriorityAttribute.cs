using System;

namespace VirtoCommerce.AzureSearchModule.Tests;

[AttributeUsage(AttributeTargets.Method)]
public class PriorityAttribute(int priority) : Attribute
{
    public int Priority { get; } = priority;
}
