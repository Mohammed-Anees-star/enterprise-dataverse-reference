using EnterpriseTicketing.Domain.Enums;
using FluentValidation;

namespace EnterpriseTicketing.Application.Tickets.Commands.CreateTicket;

public sealed class CreateTicketCommandValidator : AbstractValidator<CreateTicketCommand>
{
    public CreateTicketCommandValidator()
    {
        RuleFor(x => x.Title)
            .NotEmpty().WithMessage("Title is required.")
            .MaximumLength(200).WithMessage("Title cannot exceed 200 characters.");

        RuleFor(x => x.Description)
            .NotEmpty().WithMessage("Description is required.")
            .MaximumLength(4000).WithMessage("Description cannot exceed 4000 characters.");

        RuleFor(x => x.Priority)
            .IsInEnum().WithMessage("Invalid ticket priority value.");

        RuleFor(x => x.Category)
            .IsInEnum().WithMessage("Invalid ticket category value.");

        RuleFor(x => x.CustomerId)
            .NotEmpty().WithMessage("Customer ID is required.");

        RuleFor(x => x.AssignedToUserId)
            .MaximumLength(100).WithMessage("Assigned user ID cannot exceed 100 characters.")
            .When(x => x.AssignedToUserId is not null);
    }
}
