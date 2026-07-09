-- Seed notification master rules (Phase I: role_id NULL = all users).
-- Run against wh_gold1 (FabricWarehouse:SubjectiveInputDatabase).
DELETE FROM [wh_gold1].[subjective_input].[notification_master]
WHERE [screen_name] IN (
    'Loan Attribute Assignment',
    'Default Date Capture',
    'LTV Validation'
);
GO

INSERT INTO [wh_gold1].[subjective_input].[notification_master]
    ([role_id], [screen_name], [screen_attribute], [table_name], [column_name], [is_active])
VALUES
    (NULL, 'Loan Attribute Assignment', 'Ranking', 'loan_alias_relationship', 'loan_ranking', 1),
    (NULL, 'Default Date Capture', 'Default Date', 'loan_alias_relationship', 'default_date', 1),
    (NULL, 'LTV Validation', 'Confirm LTV', 'loan_alias_relationship', 'current_loan_to_value', 1);
GO
