namespace Jehualli.TicketSystem.Api.Models;

public enum UserRole { Admin, Agent, Developer, Client }

public enum TicketStatus { Open, InProgress, Pending, Resolved, Closed }

public enum TicketPriority { Low, Medium, High, Critical }

public enum TicketCategory { Bug, Feature, Support, Billing, Other }
