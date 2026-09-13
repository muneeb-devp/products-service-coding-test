using FluentValidation;
using MediatR;
using Products.Application.Common.Behaviours;
using ValidationException = Products.Application.Common.Exceptions.ValidationException;

namespace Products.Application.Tests.Common;

public class ValidationBehaviourTests
{
    public sealed record TestRequest(string? Name, int Age) : IRequest<string>;

    private sealed class NameValidator : AbstractValidator<TestRequest>
    {
        public NameValidator() =>
            RuleFor(x => x.Name).NotEmpty().WithMessage("Name is required.");
    }

    private sealed class AgeValidator : AbstractValidator<TestRequest>
    {
        public AgeValidator() =>
            RuleFor(x => x.Age).GreaterThan(0).WithMessage("Age must be positive.");
    }

    private static readonly RequestHandlerDelegate<string> Next = _ => Task.FromResult("handled");

    [Fact]
    public async Task Passes_through_when_no_validators_are_registered()
    {
        var sut = new ValidationBehaviour<TestRequest, string>([]);

        var result = await sut.Handle(new TestRequest("ok", 1), Next, CancellationToken.None);

        result.Should().Be("handled");
    }

    [Fact]
    public async Task Passes_through_when_every_validator_succeeds()
    {
        var sut = new ValidationBehaviour<TestRequest, string>(
            [new NameValidator(), new AgeValidator()]);

        var result = await sut.Handle(new TestRequest("ok", 30), Next, CancellationToken.None);

        result.Should().Be("handled");
    }

    [Fact]
    public async Task Aggregates_failures_from_every_validator_into_one_exception()
    {
        // The client should learn about both problems from a single request.
        var sut = new ValidationBehaviour<TestRequest, string>(
            [new NameValidator(), new AgeValidator()]);

        var act = () => sut.Handle(new TestRequest(null, 0), Next, CancellationToken.None);

        var thrown = await act.Should().ThrowAsync<ValidationException>();

        thrown.Which.Errors.Should().ContainKeys("Name", "Age");
        thrown.Which.Errors["Name"].Should().ContainSingle().Which.Should().Be("Name is required.");
        thrown.Which.Errors["Age"].Should().ContainSingle().Which.Should().Be("Age must be positive.");
    }

    [Fact]
    public async Task Does_not_invoke_the_handler_when_validation_fails()
    {
        var handlerRan = false;
        var sut = new ValidationBehaviour<TestRequest, string>([new NameValidator()]);

        RequestHandlerDelegate<string> next = _ =>
        {
            handlerRan = true;
            return Task.FromResult("handled");
        };

        await Assert.ThrowsAsync<ValidationException>(
            () => sut.Handle(new TestRequest(null, 1), next, CancellationToken.None));

        handlerRan.Should().BeFalse("the handler must never see invalid input");
    }

    [Fact]
    public async Task Deduplicates_identical_messages_for_the_same_property()
    {
        var sut = new ValidationBehaviour<TestRequest, string>(
            [new NameValidator(), new NameValidator()]);

        var act = () => sut.Handle(new TestRequest(null, 1), Next, CancellationToken.None);

        var thrown = await act.Should().ThrowAsync<ValidationException>();

        thrown.Which.Errors["Name"].Should().ContainSingle();
    }
}
