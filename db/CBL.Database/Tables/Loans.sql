CREATE TABLE [dbo].[Loans]
(
    [Id]              UNIQUEIDENTIFIER NOT NULL,
    [BorrowerId]      UNIQUEIDENTIFIER NOT NULL,
    [Amount]          DECIMAL(18, 2)   NOT NULL,
    [TermMonths]      INT              NOT NULL,
    [Status]          NVARCHAR(20)     NOT NULL CONSTRAINT [DF_Loans_Status] DEFAULT 'Pending',
    [CreatedAt]       DATETIME2(0)     NOT NULL,
    [DecidedByUserId] UNIQUEIDENTIFIER NULL,
    [DecidedAt]       DATETIME2(0)     NULL,
    [Version]         INT              NOT NULL CONSTRAINT [DF_Loans_Version] DEFAULT 1,
    CONSTRAINT [PK_Loans] PRIMARY KEY CLUSTERED ([Id]),
    CONSTRAINT [FK_Loans_Borrower] FOREIGN KEY ([BorrowerId]) REFERENCES [dbo].[Users] ([Id]),
    CONSTRAINT [FK_Loans_DecidedBy] FOREIGN KEY ([DecidedByUserId]) REFERENCES [dbo].[Users] ([Id]),
    CONSTRAINT [CK_Loans_Amount] CHECK ([Amount] > 0),
    CONSTRAINT [CK_Loans_TermMonths] CHECK ([TermMonths] BETWEEN 1 AND 60),
    CONSTRAINT [CK_Loans_Status] CHECK ([Status] IN ('Pending', 'Approved', 'Rejected'))
);
GO

CREATE NONCLUSTERED INDEX [IX_Loans_BorrowerId_Status] ON [dbo].[Loans] ([BorrowerId], [Status]);
GO

CREATE NONCLUSTERED INDEX [IX_Loans_Status_CreatedAt] ON [dbo].[Loans] ([Status], [CreatedAt]);
GO
