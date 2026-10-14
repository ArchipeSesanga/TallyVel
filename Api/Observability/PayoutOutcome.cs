namespace Api.Observability;

public static class PayoutOutcome
{
    public const string Succeeded = "succeeded";
    public const string CycleNotFound = "cycle_not_found";
    public const string RecipientNotMember = "recipient_not_member";
    public const string NothingToPayOut = "nothing_to_pay_out";
    public const string AlreadyPaidOut = "already_paid_out";
    public const string Error = "error";

    // Maps the codes EfCycleRepository throws. Anything new falls into "error",
    // so the set can never grow by accident.
    public static string From(TallyVelException ex) => ex.Code switch
    {
        "cycle-not-found" => CycleNotFound,
        "recipient-not-member" => RecipientNotMember,
        "nothing-to-pay-out" => NothingToPayOut,
        "cycle-already-paid-out" => AlreadyPaidOut,
        _ => Error,
    };
}