# Contact — Migration Mapping (eBizWiz → EdifyBiz)

Client module **Party Master** (Contact). Read as: what the client sees → where it sits in the client DB → which column it goes to in our DB → how it shows in our UI.

Source tables: `mstparty` (party header), `msdparty` (contact persons).
Target tables: `contact` (header), `mltaddress` (address/branch), `mltcontact` (contact persons).
Scope: offices 2 (HO Mumbai), 3 (Bangalore), 4 (Delhi), 6 (Kolkata). Test offices 1 & 5 excluded.

---

## 1) Party Master → `contact` + `mltaddress`

| Client UI (eBizWiz) | Client DB (`mstparty`) | Our DB (target column) | Our UI (EdifyBiz) |
|---|---|---|---|
| Name | `vname` | `contact.name` | Name (title prefixed into the name) |
| Title | `ntitle` → `mstfixedselection` | prepended into `contact.name` | shown as part of Name (Title dropdown itself stays blank) |
| Party Type | `npartytype` → `mstpartytype.vname` | `contact.ctype` | Party Type (dropdown) |
| Party Profile | `npartyprofile` → `mstpartyprofile.vname` | `contact.companygroup` | Party Profile (dropdown) |
| City | `ncity` → `mstcity.vname` | `mltaddress.citycode` | City |
| Route (Area) | `nroute` → `mstroute.vname` | `mltaddress.area` | Route / Area |
| Sales Person | `nusersales` → `mstusers.vname` → `users.code` | `contact.crmagent` | Sales Person |
| Service Person | `nuserservice` → `mstusers.vname` → `users.code` | `contact.fieldagent` | Service Person |
| Address | `vinstaddress` | `mltaddress.address` (install row: isshipto=1, isdefault=1) | Address |
| Postal Code | `vinstpostalcode` | `mltaddress.pincode` | Postal Code |
| Telephone 1 | `vinsttel1` | `mltaddress.telephone1` | Telephone 1 |
| Telephone 2 | `vinsttel2` | `mltaddress.telephone2` | Telephone 2 |
| Fax | `vinstfax` | `mltaddress.fax1` | Fax |
| Email | `vinstemail` | `mltaddress.email1` | Email |
| Website | `vwebsite` | `mltaddress.website1` | Website |
| Billing Address (+ its postal/tel1/tel2/fax/email) | `vbilladdress`, `vbill*` | 2nd `mltaddress` row (isbillto=1) | Billing Address — only when present and different from Address |
| Tax1 | `vtax1` | `contact.pan` | Tax1 |
| Tax2 | `vtax2` | `contact.gst_no` | Tax2 |
| Tax3 | `vtax3` | `contact.cst` | Tax3 |
| Reminder Message | `vremindermessage` | `contact.custom1` | Reminder Message |
| Remarks | `vremarks` | `contact.remark` | Remarks (Tax1/Tax2/Tax3 also appended here as a lossless backup) |
| Active | `bactive` | `contact.isdeleted` | Active (inverted: active=1 → isdeleted=0) |
| (office of the record) | `nofficeid` | `contact.companybranch` | company branch (per office) |

Notes:
- Tax1/2/3 in the client are free-text labelled fields (e.g. "GSTIN - ...", "PAN - ...", "IEC No - ..."), not clean columns. Target `pan`/`gst_no`/`cst` widened to `nvarchar(50)` so nothing truncates; full original also kept in Remarks.
- Sales/Service person: matched to a target `users` row by name; created if missing.

---

## 2) Party Detail (Contact Persons) → `mltcontact`

| Client UI (eBizWiz) | Client DB (`msdparty`) | Our DB (`mltcontact`) | Our UI (EdifyBiz) |
|---|---|---|---|
| Contact Person | `vcontactperson` | `name` | Contact Person (title prefixed into the name) |
| Title | `ntitle` → `mstfixedselection` | prepended into `name` | shown as part of Contact Person |
| Designation | `ndesignation` → `mstdesignation.vname` (else `vdesignation`) | `designation` | Designation |
| Mobile | `vmobile` | `mobile1` | Mobile (`person_form_mobile1`) |
| Telephone 1 | `vtel1` | `telephone1` | Telephone 1 |
| Telephone 2 | `vtel2` | `telephone2` | Telephone 2 |
| Branch | (no source field) — the **parent contact's default (install) `mltaddress`** | `mltcontact.branch` | Branch (`person_form_branch` dropdown — parent's branch) |
| Email | `vemail` | `email1` | Email |
| Date of Birth | `ddob` | `bday` | Date of Birth |
| Anniversary Date | `danniversary` | `anniversary` | Anniversary Date |
| Spouse (DOB) | `dspousedob` | `spousebday` | Spouse Date of Birth |
| Spouse Name | `vspousename` | `otherinfo` (prefixed "Spouse: ") | folded into Remarks (no dedicated column) |
| Communication Model | `ncommunication` → `mstfixedselection` | `communication` | Communication Model |
| Child 1 Date of Birth | `dchild1dob` | `firstchildbday` | Child 1 Date of Birth |
| Child 2 Date of Birth | `dchild2dob` | `secondchildbday` | Child 2 Date of Birth |
| Remarks | `vremarks` | `otherinfo` | Remarks |

Notes:
- **Phone:** source semantics are kept — `vmobile` → `mobile1`, `vtel1`/`vtel2` → `telephone1`/`telephone2`. `vtel1` usually holds a LANDLINE, so it is NOT promoted into Mobile (a person with only a landline correctly shows it in Telephone, Mobile blank).
- **Branch:** eBizWiz has no per-person branch; the exe sets `mltcontact.branch` = the **parent contact's default (install) `mltaddress` code** so the person's Branch dropdown shows the parent's branch (per senior).

---

## Not migrated (client fields with no target home)

| Client UI | Client DB | Reason |
|---|---|---|
| Child 1 / Child 2 Name | `vchild1` / `vchild2` | `mltcontact` has no child-name column (only the DOBs migrate) |
| Person Active | `msdparty.bactive` | not migrated — all persons kept active |
| TPC | `msdparty.btpc` | intentionally skipped (senior) |
| Person Department | — | eBizWiz has no such field |
