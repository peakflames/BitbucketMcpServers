namespace BitbucketRemoteMcpServer.Tests.Broker;

public class PkceHelperTests
{
    [Fact]
    public void VerifyS256_AcceptsTheMatchingVerifier()
    {
        var verifier = PkceHelper.GenerateCodeVerifier();
        var challenge = PkceHelper.ComputeS256Challenge(verifier);

        Assert.True(PkceHelper.VerifyS256(verifier, challenge));
    }

    [Fact]
    public void VerifyS256_RejectsAWrongVerifier()
    {
        var challenge = PkceHelper.ComputeS256Challenge(PkceHelper.GenerateCodeVerifier());

        Assert.False(PkceHelper.VerifyS256(PkceHelper.GenerateCodeVerifier(), challenge));
    }

    [Fact]
    public void GenerateCodeVerifier_ProducesDistinctValuesEachCall()
    {
        Assert.NotEqual(PkceHelper.GenerateCodeVerifier(), PkceHelper.GenerateCodeVerifier());
    }
}
