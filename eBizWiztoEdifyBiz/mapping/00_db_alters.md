# 00 — Database changes (ALTER queries)

Every schema change the migration needs on the **saksham tenant database**.
Nothing here exists in stock EdifyBiz: each item below was verified absent from the
cona, lopa and yash tenant databases, so no other company is affected.

Run this file **before** the migration exe, on the saksham database only.

---

## 1. New columns

| # | Table | Column | Type | Used by | Why |
|---|---|---|---|---|---|
| 1 | `inqcs` | `stage` | int | Customer Inquiry | Sales Stage → `miscellaneous(Inquiry/Sales Stage)` |
| 2 | `inqcs` | `inqprobability` | int | Customer Inquiry | Probability % of getting the order → `miscellaneous(Inquiry/Probability)` |
| 3 | `inqcs` | `inqvalue` | int | Customer Inquiry | Inquiry Value → `miscellaneous(Inquiry/Inquiry Value)` |
| 4 | `inqcs` | `quotetype` | int | Customer Quotation | Quote Type → `miscellaneous(Inquiry/Quote Type)` |
| 5 | `inqcs` | `enqrefno` | nvarchar(200) | Customer Quotation | Enquiry / Tender Ref No |
| 6 | `inqcs` | `enqrefdate` | datetime | Customer Quotation | Enquiry / Tender Ref Date |
| 7 | `inqcs` | `followupdate` | datetime | Customer Quotation | Follow Up Date |
| 8 | `inqcs` | `pendingreason` | int | Sales Order | Pending Reason → `miscellaneous(Purchase Order/Pending Reason)` |
| 9 | `followup` | `priority` | int | Inquiry follow-up | Action priority |
| 10 | `pur_order` | `pendingreason` | int | Purchase Order | Pending Reason → `miscellaneous(Purchase Order/Pending Reason)` |
| 11 | `pur_order` | `inquirycategory` | int | Purchase Order | Inquiry Category |
| 12 | `pur_order` | `invoiceno` | nvarchar(100) | Purchase Order | Supplier invoice number |
| 13 | `pur_order` | `invoicedate` | datetime | Purchase Order | Supplier invoice date |
| 14 | `pur_order` | `oano` | nvarchar(100) | Purchase Order | Order acknowledgement number |
| 15 | `pur_order` | `oadate` | datetime | Purchase Order | Order acknowledgement date |

```sql
IF COL_LENGTH('inqcs','stage')             IS NULL ALTER TABLE inqcs     ADD stage           int NULL;
IF COL_LENGTH('inqcs','inqprobability')    IS NULL ALTER TABLE inqcs     ADD inqprobability  int NULL;
IF COL_LENGTH('inqcs','inqvalue')          IS NULL ALTER TABLE inqcs     ADD inqvalue        int NULL;
IF COL_LENGTH('inqcs','quotetype')         IS NULL ALTER TABLE inqcs     ADD quotetype       int NULL;
IF COL_LENGTH('inqcs','enqrefno')          IS NULL ALTER TABLE inqcs     ADD enqrefno        nvarchar(200) NULL;
IF COL_LENGTH('inqcs','enqrefdate')        IS NULL ALTER TABLE inqcs     ADD enqrefdate      datetime NULL;
IF COL_LENGTH('inqcs','followupdate')      IS NULL ALTER TABLE inqcs     ADD followupdate    datetime NULL;
IF COL_LENGTH('inqcs','pendingreason')     IS NULL ALTER TABLE inqcs     ADD pendingreason   int NULL;

IF COL_LENGTH('followup','priority')       IS NULL ALTER TABLE followup  ADD priority        int NULL;

IF COL_LENGTH('pur_order','pendingreason')   IS NULL ALTER TABLE pur_order ADD pendingreason   int NULL;
IF COL_LENGTH('pur_order','inquirycategory') IS NULL ALTER TABLE pur_order ADD inquirycategory int NULL;
IF COL_LENGTH('pur_order','invoiceno')       IS NULL ALTER TABLE pur_order ADD invoiceno       nvarchar(100) NULL;
IF COL_LENGTH('pur_order','invoicedate')     IS NULL ALTER TABLE pur_order ADD invoicedate     datetime NULL;
IF COL_LENGTH('pur_order','oano')            IS NULL ALTER TABLE pur_order ADD oano            nvarchar(100) NULL;
IF COL_LENGTH('pur_order','oadate')          IS NULL ALTER TABLE pur_order ADD oadate          datetime NULL;
```

---

## 2. Widened columns

The eBizWiz value does not fit the stock EdifyBiz column. Widening only — no data is lost,
and a shorter value still stores exactly as before.

| # | Table.column | Stock EdifyBiz | Saksham | Reason |
|---|---|---|---|---|
| 1 | `contact.pan` | nvarchar(30) | nvarchar(50) | Party Tax1 is free text, not a bare PAN |
| 2 | `contact.gst_no` | varchar(15) | nvarchar(50) | Party Tax2 is free text, not a bare GSTIN |
| 3 | `contact.cst` | nvarchar(30) | nvarchar(50) | Party Tax3 is free text |
| 4 | `followup.followType` | nvarchar(50) | nvarchar(200) | eBizWiz action type names are longer |
| 5 | `followup.person` | nvarchar(50) | nvarchar(200) | Person contacted names are longer |
| 6 | `inqcs.sources` | varchar(50) | varchar(250) | Inquiry Source names up to ~90 chars |
| 7 | `pur_order.refwono` | nvarchar(100) | nvarchar(255) | Supplier reference numbers are longer |
| 8 | `sal_adjust.adjustname` | varchar(20) | varchar(100) | Post-tax charge names (freight etc.) |
| 9 | `sal_order_det.installedremarks` | nvarchar(500) | nvarchar(1000) | Holds the machine serial numbers |

```sql
ALTER TABLE contact       ALTER COLUMN pan               nvarchar(50)   NULL;
ALTER TABLE contact       ALTER COLUMN gst_no            nvarchar(50)   NULL;
ALTER TABLE contact       ALTER COLUMN cst               nvarchar(50)   NULL;
ALTER TABLE followup      ALTER COLUMN followType        nvarchar(200)  NULL;
ALTER TABLE followup      ALTER COLUMN person            nvarchar(200)  NULL;
ALTER TABLE inqcs         ALTER COLUMN sources           varchar(250)   NULL;
ALTER TABLE pur_order     ALTER COLUMN refwono           nvarchar(255)  NULL;
ALTER TABLE sal_adjust    ALTER COLUMN adjustname        varchar(100)   NULL;
ALTER TABLE sal_order_det ALTER COLUMN installedremarks  nvarchar(1000) NULL;
```

---

## 3. Notes

- **Run order.** SQL Server aborts the whole batch on the first failed ALTER, so run section 1
  and section 2 as two separate batches and read the messages pane before continuing.
- **Application code.** Every place the EdifyBiz code reads these columns is inside a
  `Session("companysname") = "saksham"` gate, so the other tenant databases do not need them.
- **Re-running the exe.** The exe clears only what an earlier run of itself wrote; it never
  drops or re-creates columns. This file is applied once per fresh database.
- **Verifying.** To re-derive this list on any machine, compare the saksham tenant database
  against a stock one (cona / lopa / yash): a column present only in saksham, or wider in
  saksham, belongs in this file.
