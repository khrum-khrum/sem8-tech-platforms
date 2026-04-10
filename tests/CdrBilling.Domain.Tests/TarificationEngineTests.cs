using CdrBilling.Domain.Entities;
using CdrBilling.Domain.Enums;
using CdrBilling.Domain.Services;

namespace CdrBilling.Domain.Tests;

public sealed class TarificationEngineTests
{
    private static readonly DateOnly AnyDate = new(2026, 1, 1);

    [Fact]
    public void NonAnswered_Call_IsNotTariffed()
    {
        var engine = new TarificationEngine([CreateTariff()]);

        var match = engine.FindBestTariff(CreateCall(Disposition.Busy, billableSec: 120));

        Assert.Null(match);
    }

    [Fact]
    public void Answered_Call_Uses_Billable_Duration_Not_Total_Duration()
    {
        var engine = new TarificationEngine([CreateTariff(ratePerMin: 2m, connectionFee: 1m)]);

        var match = engine.FindBestTariff(CreateCall(Disposition.Answered, billableSec: 61));

        Assert.NotNull(match);
        Assert.Equal(5.00m, match!.Value.Charge);
    }

    [Fact]
    public void ConnectionFee_IsApplied_Only_For_Answered_Call()
    {
        var engine = new TarificationEngine([CreateTariff(ratePerMin: 0m, connectionFee: 3m)]);

        var answered = engine.FindBestTariff(CreateCall(Disposition.Answered, billableSec: 1));
        var failed = engine.FindBestTariff(CreateCall(Disposition.Failed, billableSec: 1));

        Assert.NotNull(answered);
        Assert.Equal(3.00m, answered!.Value.Charge);
        Assert.Null(failed);
    }

    [Theory]
    [InlineData(1, 60)]
    [InlineData(60, 60)]
    [InlineData(61, 120)]
    [InlineData(119, 120)]
    [InlineData(121, 180)]
    public void Billable_Duration_IsRounded_Up_To_Full_Minute(int inputBillableSec, int expectedRoundedSec)
    {
        var engine = new TarificationEngine([CreateTariff(ratePerMin: 1m, connectionFee: 0m)]);

        var match = engine.FindBestTariff(CreateCall(Disposition.Answered, billableSec: inputBillableSec));

        Assert.NotNull(match);
        Assert.Equal(expectedRoundedSec / 60m, match!.Value.Charge);
    }

    [Fact]
    public void Calculated_Amount_IsRounded_To_Kopecks()
    {
        var engine = new TarificationEngine([CreateTariff(ratePerMin: 0.3333m, connectionFee: 0m)]);

        var match = engine.FindBestTariff(CreateCall(Disposition.Answered, billableSec: 61));

        Assert.NotNull(match);
        Assert.Equal(0.67m, match!.Value.Charge);
    }

    [Fact]
    public void Incoming_Call_IsNotTariffed()
    {
        var engine = new TarificationEngine([CreateTariff()]);

        var match = engine.FindBestTariff(CreateCall(Disposition.Answered, billableSec: 120, direction: CallDirection.Incoming));

        Assert.Null(match);
    }

    private static TariffEntry CreateTariff(decimal ratePerMin = 1m, decimal connectionFee = 0m) =>
        TariffEntry.Create(
            sessionId: Guid.NewGuid(),
            prefix: "79",
            destination: "Test",
            ratePerMin: ratePerMin,
            connectionFee: connectionFee,
            timebandStart: new TimeOnly(0, 0),
            timebandEnd: new TimeOnly(23, 59),
            weekdayMask: DayOfWeekMask.All,
            priority: 1,
            effectiveDate: AnyDate,
            expiryDate: null);

    private static TarificationCall CreateCall(
        Disposition disposition,
        int billableSec,
        CallDirection direction = CallDirection.Outgoing) =>
        new(
            Id: 1,
            StartTime: new DateTimeOffset(2026, 2, 2, 10, 0, 0, TimeSpan.Zero),
            Direction: direction,
            Disposition: disposition,
            BillableSec: billableSec,
            DigitsToRate: "79001234567");
}
