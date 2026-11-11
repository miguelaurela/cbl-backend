CREATE TABLE [dbo].[SystemParameters]
(
    [Key]             NVARCHAR(100)    NOT NULL,
    [Value]           NVARCHAR(500)    NOT NULL,
    [UpdatedAt]       DATETIME2(0)     NOT NULL,
    [UpdatedByUserId] UNIQUEIDENTIFIER NULL,
    CONSTRAINT [PK_SystemParameters] PRIMARY KEY CLUSTERED ([Key]),
    CONSTRAINT [FK_SystemParameters_UpdatedBy] FOREIGN KEY ([UpdatedByUserId]) REFERENCES [dbo].[Users] ([Id])
);
