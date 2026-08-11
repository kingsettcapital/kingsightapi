-- Seed subjective_input user/role master (sequential ids 1, 2, 3…).
DELETE FROM [subjective_input].[user_master];
DELETE FROM [subjective_input].[role_master];
GO

INSERT INTO [subjective_input].[role_master]
    ([role_id], [role_name], [status])
VALUES
    (1, 'admin', 'A'),
    (2, 'Kingsett User', 'A'),
    (3, 'User A', 'A');
GO

INSERT INTO [subjective_input].[user_master]
    ([user_id], [role_id], [email], [first_name], [last_name], [is_active], [created_datetime], [created_by])
VALUES
    (1, 1, 'sgarikapati@kingsettcapital.com', 'Sridevi', 'Garikapati', 'Y', '2026-06-23T17:23:03.666680', 'system'),
    (2, 1, 'KHendricksen@kingsettcapital.com', 'Karen', 'Hendricksen', 'Y', '2026-06-23T17:23:36.501606', 'system'),
    (3, 1, 'SKumar@kingsettcapital.com', 'Kumar', 'Krishnasamy', 'Y', '2026-06-23T17:24:07.693831', 'system'),
    (4, 1, 'SKannan@kingsettcapital.com', 'Suresh', 'Kannan', 'Y', '2026-06-23T17:24:26.538518', 'system'),
    (5, 1, 'JShen@kingsettcapital.com', 'Janice', 'Shen', 'Y', '2026-06-23T17:25:00.562125', 'system');
GO
