namespace Infrastructure.Helpers;

/// <summary>
/// DOTS score: lifted weight normalised by bodyweight, so strength can be compared
/// across different bodyweights (and across your own bulks/cuts).
/// DOTS = weight * 500 / (a*bw^4 + b*bw^3 + c*bw^2 + d*bw + e)
/// </summary>
public static class DotsCalculator
{
    private static readonly double[] Male = [-0.0000010930, 0.0007391293, -0.1918759221, 24.0900756, -307.75076];
    private static readonly double[] Female = [-0.0000010706, 0.0005158568, -0.1126655495, 13.6175032, -57.96288];

    public static decimal Calculate(decimal liftedKg, double bodyweightKg, bool female = false)
    {
        if (liftedKg <= 0 || bodyweightKg <= 0) return 0m;

        var c = female ? Female : Male;
        // Official DOTS clamps bodyweight to the range the polynomial was fit on.
        var bw = Math.Clamp(bodyweightKg, 40.0, female ? 150.0 : 210.0);

        var denominator = c[0] * Math.Pow(bw, 4)
                          + c[1] * Math.Pow(bw, 3)
                          + c[2] * Math.Pow(bw, 2)
                          + c[3] * bw
                          + c[4];

        return liftedKg * (decimal)(500.0 / denominator);
    }
}