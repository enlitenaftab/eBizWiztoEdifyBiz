-- ============================================================================
--  eBizWiz -> EdifyBiz  |  Run these ALTERs on the FRESH-restored TARGET DB
--  (SakshamRMtP15329) BEFORE running the migration exe.
--
--  Scope: Contact, Product, Customer Inquiry (+ its follow-ups).
--
--  Customer Inquiry follow-ups write ONLY NATIVE followup columns
--  (person=pcode=mltcontact.code, remarkscode=followupremarks, followType=Type,
--   purpose=to-be activity, sales person=createdby, priority=native priority master).
--  The custom columns (actiontype/purposeremarks/partycontact/salesperson) are GONE.
--
--  Two changes below:
--   1. followup.priority — ADD it. The native app (app/followup.asp) already expects
--      followup.priority (joins the `priority` master), but this DB's followup lacks the
--      column; add it so priority is a proper native field (High/Medium/Low), per senior.
--   2. contact pan/gst_no/cst — widen 3 EXISTING native tax columns (too small).
--  SAFETY: one additive column + widen. No row data touched.
-- ============================================================================

------------------------------------------------------------------------------
-- FOLLOWUP — native priority column (INT, FK to the `priority` master: High=1/Medium=2/Low=3...)
------------------------------------------------------------------------------
IF COL_LENGTH('followup','priority') IS NULL ALTER TABLE followup ADD priority int NULL;

------------------------------------------------------------------------------
-- INQCS — Sales Stage column (INT, FK to miscellaneous(Inquiry/Sales Stage)); moved out of remark.
-- (Lost Reason needs NO ALTER: inqcs.lossreason already exists natively.)
------------------------------------------------------------------------------
IF COL_LENGTH('inqcs','stage') IS NULL ALTER TABLE inqcs ADD stage int NULL;

------------------------------------------------------------------------------
-- CONTACT — tax fields
-- Source has NO clean PAN/GST columns: only 3 free-form labelled fields
-- (vtax1/vtax2/vtax3 = "GSTIN - 24AAACC6253G1ZZ", "IEC No - ...", "PAN - ...").
-- Native widths chopped them (pan/cst nvarchar(30), gst_no varchar(15)):
--   gst_no chopped the GSTIN on 53 rows; pan chopped 4 (values up to 49 chars).
-- Widen all three to nvarchar(50); the exe caps to 50 (in-scope max 49/41/26 -> no truncation).
-- Full originals are also kept in contact.remark.
------------------------------------------------------------------------------
ALTER TABLE contact ALTER COLUMN pan    nvarchar(50);
ALTER TABLE contact ALTER COLUMN gst_no nvarchar(50);
ALTER TABLE contact ALTER COLUMN cst    nvarchar(50);

------------------------------------------------------------------------------
-- INQCS — Customer Quotation structured fields.
-- These are STANDARD EdifyBiz inqcs columns that already exist for other companies
-- (Akpowertech), but not in this saksham DB. We enable the same fields for saksham
-- (form ungated + view shown) so quotation data is structured, not crammed in remark:
--   quotetype  -> Quote Type   (INT, FK to miscellaneous(Inquiry/Quote Type))
--   enqrefno   -> Ref No        ("Enquiry/Tender Ref No" — free text)
--   enqrefdate -> Ref Date
-- (currency/exchangerate/inqlink/lossreason/Category already exist natively — no ALTER.)
------------------------------------------------------------------------------
IF COL_LENGTH('inqcs','quotetype')  IS NULL ALTER TABLE inqcs ADD quotetype  int NULL;
IF COL_LENGTH('inqcs','enqrefno')   IS NULL ALTER TABLE inqcs ADD enqrefno   nvarchar(200) NULL;
IF COL_LENGTH('inqcs','enqrefdate') IS NULL ALTER TABLE inqcs ADD enqrefdate datetime NULL;

------------------------------------------------------------------------------
-- INQCS — Sales Order Bill To / Ship To addresses.
-- Source party addresses (mstparty.vbilladdress / vinstaddress) are nvarchar(1000);
-- native inqcs bilingaddress/deliveryaddress are only nvarchar(200) and chop long addresses.
-- Widen both to nvarchar(1000) so the full Bill To / Ship To text stores without truncation.
------------------------------------------------------------------------------
ALTER TABLE inqcs ALTER COLUMN bilingaddress   nvarchar(1000);
ALTER TABLE inqcs ALTER COLUMN deliveryaddress nvarchar(1000);

------------------------------------------------------------------------------
-- INQCS — Customer Quotation "Follow Up Date".
-- eBizWiz quotation has its own Follow Up Date (dfollowupdate). It must NOT go into
-- validdate (that is the quotation's own validity date, a standard field). Add a
-- dedicated column so the follow-up date is stored separately (per senior).
------------------------------------------------------------------------------
IF COL_LENGTH('inqcs','followupdate') IS NULL ALTER TABLE inqcs ADD followupdate datetime NULL;

------------------------------------------------------------------------------
-- PUR_ORDER — Purchase Order (Order Placed) new columns.
-- Reuse existing where present (purorderno, purorderdt, scode, currency, discount,
-- deliverydate, pay_notes, remarks, status, salesordercode, branchcode, executive).
-- These 8 have no home in pur_order → add clean new columns (all lower-case, non-reserved):
--   inquirycategory   -> Inquiry Category (INT, FK miscellaneous(Inquiry/Category))
--   pendingreason     -> Pending Reason (INT, FK miscellaneous(Purchase Order/Pending Reason))
--   invoiceno/date    -> Invoice No. / Invoice Date
--   oano/oadate       -> O/A No. / O/A Date
-- Bill To / Ship To are stored as BRANCH REFERENCES (mltaddress code) — Bill To = existing pur_order.cbranchcode,
-- Ship To = existing pur_order.shippingbranch. NO new branch column needed (both already exist).
------------------------------------------------------------------------------
IF COL_LENGTH('pur_order','inquirycategory') IS NULL ALTER TABLE pur_order ADD inquirycategory int            NULL;
IF COL_LENGTH('pur_order','pendingreason')   IS NULL ALTER TABLE pur_order ADD pendingreason   int            NULL;
IF COL_LENGTH('pur_order','invoiceno')       IS NULL ALTER TABLE pur_order ADD invoiceno       nvarchar(100)  NULL;
IF COL_LENGTH('pur_order','invoicedate')     IS NULL ALTER TABLE pur_order ADD invoicedate     datetime       NULL;
IF COL_LENGTH('pur_order','oano')            IS NULL ALTER TABLE pur_order ADD oano            nvarchar(100)  NULL;
IF COL_LENGTH('pur_order','oadate')          IS NULL ALTER TABLE pur_order ADD oadate          datetime       NULL;

------------------------------------------------------------------------------
-- SAL_ORDER_DET — Sales Invoice serial numbers.
-- Serial no(s) of a line are stored as text in installedremarks ("Serial No: ..."). It is nvarchar(500);
-- a few lines carry up to 60 serials (~730 chars) → widen to nvarchar(1000). Lossless, nothing else changes.
------------------------------------------------------------------------------
IF COL_LENGTH('sal_order_det','installedremarks') IS NOT NULL
   AND (SELECT CHARACTER_MAXIMUM_LENGTH FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME='sal_order_det' AND COLUMN_NAME='installedremarks') BETWEEN 1 AND 999
   ALTER TABLE sal_order_det ALTER COLUMN installedremarks nvarchar(1000) NULL;

------------------------------------------------------------------------------
-- SAL_ADJUST — Sales Invoice post-tax charges (freight, packing, discount, round off, GST on charges ...).
-- eBizWiz charge names are up to 100 chars ("Add : Sea Freight & Insurance Charges"); adjustname is varchar(20)
-- → widen to varchar(100). Lossless, nothing else changes.
------------------------------------------------------------------------------
IF COL_LENGTH('sal_adjust','adjustname') IS NOT NULL
   AND (SELECT CHARACTER_MAXIMUM_LENGTH FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME='sal_adjust' AND COLUMN_NAME='adjustname') BETWEEN 1 AND 99
   ALTER TABLE sal_adjust ALTER COLUMN adjustname varchar(100) NULL;
