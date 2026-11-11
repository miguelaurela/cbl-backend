CREATE TABLE [dbo].[Users]
(
    [Id]           UNIQUEIDENTIFIER NOT NULL,
    [Email]        NVARCHAR(256)    NOT NULL,
    [FullName]     NVARCHAR(200)    NOT NULL,
    [PasswordHash] NVARCHAR(500)    NOT NULL,
    [Role]         NVARCHAR(20)     NOT NULL,
    [IsActive]     BIT              NOT NULL CONSTRAINT [DF_Users_IsActive] DEFAULT 1,
    [CreatedAt]    DATETIME2(0)     NOT NULL,
    CONSTRAINT [PK_Users] PRIMARY KEY CLUSTERED ([Id]),
    CONSTRAINT [UQ_Users_Email] UNIQUE ([Email]),
    CONSTRAINT [CK_Users_Role] CHECK ([Role] IN ('Admin', 'Borrower'))
);
