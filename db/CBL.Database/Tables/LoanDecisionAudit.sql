CREATE TABLE [dbo].[LoanDecisionAudit]
(
    [Id]             UNIQUEIDENTIFIER NOT NULL,
    [LoanId]         UNIQUEIDENTIFIER NOT NULL,
    [AdminUserId]    UNIQUEIDENTIFIER NOT NULL,
    [Decision]       NVARCHAR(20)     NOT NULL,
    [PreviousStatus] NVARCHAR(20)     NOT NULL,
    [DecidedAt]      DATETIME2(0)     NOT NULL,
    [CorrelationId]  NVARCHAR(64)     NULL,
    CONSTRAINT [PK_LoanDecisionAudit] PRIMARY KEY CLUSTERED ([Id]),
    CONSTRAINT [FK_LoanDecisionAudit_Loan] FOREIGN KEY ([LoanId]) REFERENCES [dbo].[Loans] ([Id]),
    CONSTRAINT [FK_LoanDecisionAudit_Admin] FOREIGN KEY ([AdminUserId]) REFERENCES [dbo].[Users] ([Id]),
    CONSTRAINT [CK_LoanDecisionAudit_Decision] CHECK ([Decision] IN ('Approved', 'Rejected'))
);
GO

CREATE NONCLUSTERED INDEX [IX_LoanDecisionAudit_LoanId] ON [dbo].[LoanDecisionAudit] ([LoanId]);
GO
