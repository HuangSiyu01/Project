namespace Project.Models
{
    public static class SD
    {
        // User roles
        public const string Role_Admin = "Admin";
        public const string Role_Customer = "Customer";

        // Order statuses
        public const string StatusPending = "Pending";
        public const string StatusApproved = "Approved";
        public const string StatusProcessing = "Processing";
        public const string StatusCompleted = "Completed";
        public const string StatusCancelled = "Cancelled";

        // Payment statuses
        public const string PaymentPending = "Pending";
        public const string PaymentPaid = "Paid";
        public const string PaymentFailed = "Failed";
        public const string PaymentRefunded = "Refunded";

        // Refund request statuses
        public const string RefundPending = "Pending";
        public const string RefundApproved = "Approved";
        public const string RefundRejected = "Rejected";
        public const string RefundCompleted = "Completed";

        // E-ticket statuses
        public const string TicketActive = "Active";
        public const string TicketRefunded = "Refunded";
    }
}