namespace Aonik.Platform.Services.Onboarding;

internal class OnboardingPolicyOptions
{
    public bool RequireEmailVerified { get; set; } = true;
    public bool RequirePhoneVerified { get; set; }
    public bool RequireProfileComplete { get; set; } = true;
    public List<string> EmailVerifiedActions { get; set; } = new() { "VerifyEmail" };
    public List<string> PhoneVerifiedActions { get; set; } = new() { "VerifyPhone" };
    public List<string> ProfileCompleteActions { get; set; } = new() { "CompleteProfile" };
}
