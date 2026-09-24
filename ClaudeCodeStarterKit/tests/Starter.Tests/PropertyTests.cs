using FsCheck;
using FsCheck.Fluent;
using FsCheck.Xunit;
using Starter.Core;

namespace Starter.Tests;

/// <summary>
/// Property-based tests: invariants that must hold for every input, not just the examples
/// a person thought of. Numeric code earns these; parsing and formatting code too.
/// </summary>
public class PropertyTests
{
    private static readonly Arbitrary<double[]> NonEmptyFiniteArrays =
        ArbMap.Default.ArbFor<double>()
            .Filter(d => double.IsFinite(d) && Math.Abs(d) < 1e12)
            .Array()
            .Filter(a => a.Length > 0);

    [Property(MaxTest = 200)]
    public Property R001_Median_LiesWithinMinAndMax()
    {
        return Prop.ForAll(NonEmptyFiniteArrays, values =>
        {
            double median = Stats.Median(values);
            return median >= values.Min() && median <= values.Max();
        });
    }

    [Property(MaxTest = 200)]
    public Property R001_Median_IsInvariantUnderReversal()
    {
        return Prop.ForAll(NonEmptyFiniteArrays, values =>
            Stats.Median(values) == Stats.Median(values.Reverse().ToArray()));
    }

    [Property(MaxTest = 200)]
    public Property R002_RobustSigma_IsNonNegative()
    {
        return Prop.ForAll(NonEmptyFiniteArrays, values => Stats.RobustSigma(values) >= 0);
    }
}
