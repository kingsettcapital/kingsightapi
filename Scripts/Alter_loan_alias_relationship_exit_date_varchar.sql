-- Default Subjective Analytics stores quarter labels (e.g. Q1/2025) in exit_date.
-- Run against wh_gold1.subjective_input when exit_date is still a date type.

alter table wh_gold1.subjective_input.loan_alias_relationship
    alter column exit_date varchar(100) null;
