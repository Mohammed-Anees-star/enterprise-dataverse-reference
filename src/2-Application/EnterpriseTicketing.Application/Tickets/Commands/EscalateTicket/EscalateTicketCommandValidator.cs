using FluentValidation;

namespace EnterpriseTicketing.Application.Tickets.Commands.EscalateTicket;

public sealed class EscalateTicketCommandValidator : AbstractValidator<EscalateTicketCommand>
{
    public EscalateTicketCommandValidator()
    {
        RuleFor(x => x.TicketId)
            .NotEmpty().WithMessage("TicketId is required.");

        RuleFor(x => x.Reason)
            .NotEmpty().WithMessage("Escalation reason is required.")
            .MaximumLength(500).WithMessage("Escalation reason cannot exceed 500 characters.");
    }
}
