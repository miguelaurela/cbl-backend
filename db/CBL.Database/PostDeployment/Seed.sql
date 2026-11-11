/*
  Datos iniciales (idempotentes: solo insertan lo que falta y nunca pisan cambios hechos después).
  Las contraseñas "123" son SOLO DE EJEMPLO para la demostración; se guardan con hash PBKDF2
  (el mismo formato que PasswordHasher de Microsoft.Extensions.Identity.Core).
  Los ids deben coincidir con CBL.Infrastructure.Persistence.SeedData.
*/
DECLARE @now DATETIME2(0) = SYSUTCDATETIME();

MERGE INTO [dbo].[Users] AS target
USING (VALUES
    (CONVERT(UNIQUEIDENTIFIER, '11111111-1111-1111-1111-111111111111'), N'usuario@test.com', N'Usuario de prueba',
     N'AQAAAAIAAYagAAAAEOjxHxoKoZxdhvVk1UpE1G86OJynfb4s1pbyFOPcDKNj7wWtu8/rcOWY+SuLOjxDIA==', N'Borrower'),
    (CONVERT(UNIQUEIDENTIFIER, '22222222-2222-2222-2222-222222222222'), N'admin@test.com', N'Administrador de prueba',
     N'AQAAAAIAAYagAAAAEOjxHxoKoZxdhvVk1UpE1G86OJynfb4s1pbyFOPcDKNj7wWtu8/rcOWY+SuLOjxDIA==', N'Admin')
) AS source ([Id], [Email], [FullName], [PasswordHash], [Role])
ON target.[Email] = source.[Email]
WHEN NOT MATCHED BY TARGET THEN
    INSERT ([Id], [Email], [FullName], [PasswordHash], [Role], [IsActive], [CreatedAt])
    VALUES (source.[Id], source.[Email], source.[FullName], source.[PasswordHash], source.[Role], 1, @now);

MERGE INTO [dbo].[SystemParameters] AS target
USING (VALUES
    (N'LoanAmountMin', N'1000'),
    (N'LoanAmountMax', N'100000')
) AS source ([Key], [Value])
ON target.[Key] = source.[Key]
WHEN NOT MATCHED BY TARGET THEN
    INSERT ([Key], [Value], [UpdatedAt], [UpdatedByUserId])
    VALUES (source.[Key], source.[Value], @now, NULL);
