-- Seed with explicit ids 1, 2, 3 (sequential).
-- Run Create_input_user_management.sql first (drops old IDENTITY tables, recreates plain INT keys).

DELETE FROM input.UserMst;
DELETE FROM input.RoleMst;

INSERT INTO input.RoleMst (RoleId, RoleName, Status)
VALUES
    (1, 'Administrator', 'A'),
    (2, 'Kingsett User', 'A'),
    (3, 'User A', 'A');

INSERT INTO input.UserMst (UserId, Email, FirstName, LastName, IsActive, DateCreated, DateModified, RoleId)
VALUES
    (1, 'sgarikapati@kingsettcapital.com', 'Sridevi', 'Garikapati', 1, '2026-06-23T17:23:03.666680', NULL, 1),
    (2, 'KHendricksen@kingsettcapital.com', 'Karen', 'Hendricksen', 1, '2026-06-23T17:23:36.501606', NULL, 1),
    (3, 'SKumar@kingsettcapital.com', 'Kumar', 'Krishnasamy', 1, '2026-06-23T17:24:07.693831', NULL, 1),
    (4, 'SKannan@kingsettcapital.com', 'Suresh', 'Kannan', 1, '2026-06-23T17:24:26.538518', NULL, 1),
    (5, 'JShen@kingsettcapital.com', 'Janice', 'Shen', 1, '2026-06-23T17:25:00.562125', NULL, 1);
