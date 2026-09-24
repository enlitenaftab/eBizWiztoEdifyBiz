-- USE DATABASE
USE SakshamRMtP15329;
GO

-- 1. USERS TABLE UPDATES
UPDATE users SET compcode = 11158;
UPDATE users SET password = 'dc6dd58ac0ac3a19';

-- 2. CONTACT TABLE ALTERATIONS (Column Widening)
ALTER TABLE contact ALTER COLUMN pan nvarchar(50) NULL;
ALTER TABLE contact ALTER COLUMN cst nvarchar(50) NULL;
ALTER TABLE contact ALTER COLUMN gst_no nvarchar(50) NULL;

-- 3. FOLLOWUP TABLE ALTERATIONS
-- New Columns
ALTER TABLE followup ADD priority int NULL;
-- Column Widening (Truncation Fix)
ALTER TABLE followup ALTER COLUMN followType nvarchar(200) NULL;
ALTER TABLE followup ALTER COLUMN person nvarchar(200) NULL;

-- 4. INQCS TABLE ALTERATIONS
-- New Columns
ALTER TABLE inqcs ADD stage int NULL;
ALTER TABLE inqcs ADD quotetype int NULL;
ALTER TABLE inqcs ADD enqrefno nvarchar(200) NULL;
ALTER TABLE inqcs ADD enqrefdate datetime NULL;
ALTER TABLE inqcs ADD followupdate datetime NULL;
-- Column Widening (Truncation Fix)
ALTER TABLE inqcs ALTER COLUMN sources VARCHAR(250) NULL;

-- 5. PUR_ORDER TABLE ALTERATIONS
-- New Columns
ALTER TABLE pur_order ADD refwono nvarchar(255) NULL;
ALTER TABLE pur_order ADD inquirycategory int NULL;
ALTER TABLE pur_order ADD pendingreason int NULL;
ALTER TABLE pur_order ADD invoiceno nvarchar(100) NULL;
ALTER TABLE pur_order ADD invoicedate datetime NULL;
ALTER TABLE pur_order ADD oano nvarchar(100) NULL;
ALTER TABLE pur_order ADD oadate datetime NULL;

-- 6. PUR_TAX TABLE ALTERATIONS
-- New Columns
ALTER TABLE pur_tax ADD purorderdetcode int NULL;

-- 7. Sal_order_det TABLE ALTERATIONS
-- Column Widening (Truncation Fix)
ALTER TABLE sal_order_det ALTER COLUMN installedremarks nvarchar(1000) NULL;

-- 8. SAL_ADJUST TABLE ALTERATIONS
-- Column Widening (Truncation Fix)
ALTER TABLE sal_adjust ALTER COLUMN adjustname varchar(100) NULL;

GO
-- 9. INDEXES ON THE AMC TABLES (same convention as the EdifyBiz tables that already have them,
--    e.g. INDXC_calendar_code). contractcall / contractdetails / callvisits / amcusedproduct /
--    taskchecklist carry the biggest migrated volumes and had no index at all, so the Complaint
--    list search timed out. - Aftab Alam
CREATE CLUSTERED INDEX INDXC_contractcall_code ON contractcall (code);
CREATE NONCLUSTERED INDEX INDXC_contractcall_contractdtcode ON contractcall (contractdtcode);
CREATE NONCLUSTERED INDEX INDXC_contractcall_ccode ON contractcall (ccode);
CREATE NONCLUSTERED INDEX INDXC_contractcall_date ON contractcall (date);
CREATE NONCLUSTERED INDEX INDXC_contractcall_pmscomplaintno ON contractcall (pmscomplaintno);

CREATE CLUSTERED INDEX INDXC_contractdetails_code ON contractdetails (code);
CREATE NONCLUSTERED INDEX INDXC_contractdetails_contractcode ON contractdetails (contractcode, productcode);

CREATE CLUSTERED INDEX INDXC_callvisits_code ON callvisits (code);
CREATE NONCLUSTERED INDEX INDXC_callvisits_contractcallcode ON callvisits (contractcallcode);

CREATE CLUSTERED INDEX INDXC_amcusedproduct_code ON amcusedproduct (code);
CREATE NONCLUSTERED INDEX INDXC_amcusedproduct_complaintcode ON amcusedproduct (complaintcode);

CREATE CLUSTERED INDEX INDXC_taskchecklist_code ON taskchecklist (code);
CREATE NONCLUSTERED INDEX INDXC_taskchecklist_module ON taskchecklist (module, modulecode);

GO
