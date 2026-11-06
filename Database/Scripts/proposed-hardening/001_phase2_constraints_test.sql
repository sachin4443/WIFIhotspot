-- Constraint tests for 001_phase2_schema.sql. Run against a scratch database after the schema script:
--   psql -v ON_ERROR_STOP=1 -d nxt_scratch -f 001_phase2_constraints_test.sql
-- Every block either passes silently or raises an exception that stops the run.

CREATE FUNCTION pg_temp.expect_fail(label text, stmt text) RETURNS void LANGUAGE plpgsql AS $$
BEGIN
    BEGIN
        EXECUTE stmt;
    EXCEPTION WHEN OTHERS THEN
        RAISE NOTICE 'PASS (rejected): %  [%]', label, SQLERRM;
        RETURN;
    END;
    RAISE EXCEPTION 'FAIL (accepted but should be rejected): %', label;
END $$;

CREATE FUNCTION pg_temp.expect_ok(label text, stmt text) RETURNS void LANGUAGE plpgsql AS $$
BEGIN
    EXECUTE stmt;
    RAISE NOTICE 'PASS (accepted): %', label;
END $$;

-- Fixtures -------------------------------------------------------------------------------------
INSERT INTO "OrganizationTypes" ("TypeName") VALUES ('Hotel'), ('School');
INSERT INTO "Organizations" ("Id","Name","OrganizationTypeId","ContactPerson","MobileNumber","Email","Status")
VALUES ('11111111-1111-1111-1111-111111111111','Org A',1,'A','9876543210','a@example.com','Active'),
       ('22222222-2222-2222-2222-222222222222','Org B',2,'B','9876543211','b@example.com','Active');
INSERT INTO "Sites" ("Id","OrganizationId","Code","Name","IsPrimary") VALUES
 ('aaaaaaaa-0000-0000-0000-000000000001','11111111-1111-1111-1111-111111111111','MAIN','Main',true),
 ('bbbbbbbb-0000-0000-0000-000000000001','22222222-2222-2222-2222-222222222222','MAIN','Main',true);

-- Organization types ---------------------------------------------------------------------------
SELECT pg_temp.expect_fail('duplicate type name', $$INSERT INTO "OrganizationTypes" ("TypeName") VALUES ('Hotel')$$);
INSERT INTO "OrganizationTypeLoginMethods" ("Id","OrganizationTypeId","AuthenticationType","IsDefault")
VALUES ('00000000-0000-0000-0000-0000000000a1',1,'RoomMobile',true);
SELECT pg_temp.expect_fail('second default login method for a type', $$INSERT INTO "OrganizationTypeLoginMethods" ("Id","OrganizationTypeId","AuthenticationType","IsDefault") VALUES ('00000000-0000-0000-0000-0000000000a2',1,'MobileOTP',true)$$);
SELECT pg_temp.expect_fail('duplicate auth type for a type', $$INSERT INTO "OrganizationTypeLoginMethods" ("Id","OrganizationTypeId","AuthenticationType") VALUES ('00000000-0000-0000-0000-0000000000a3',1,'RoomMobile')$$);
SELECT pg_temp.expect_ok('non-default second method', $$INSERT INTO "OrganizationTypeLoginMethods" ("Id","OrganizationTypeId","AuthenticationType") VALUES ('00000000-0000-0000-0000-0000000000a4',1,'MobileOTP')$$);

-- Organizations --------------------------------------------------------------------------------
SELECT pg_temp.expect_fail('invalid organization status', $$INSERT INTO "Organizations" ("Id","Name","OrganizationTypeId","ContactPerson","MobileNumber","Email","Status") VALUES (gen_random_uuid(),'X',1,'A','1','a@b.c','Bogus')$$);
SELECT pg_temp.expect_fail('unknown organization type', $$INSERT INTO "Organizations" ("Id","Name","OrganizationTypeId","ContactPerson","MobileNumber","Email","Status") VALUES (gen_random_uuid(),'X',999,'A','1','a@b.c','Active')$$);
SELECT pg_temp.expect_ok('two orgs may share a name', $$INSERT INTO "Organizations" ("Id","Name","OrganizationTypeId","ContactPerson","MobileNumber","Email","Status") VALUES (gen_random_uuid(),'Org A',1,'A','1','a@b.c','Active')$$);

-- Sites ----------------------------------------------------------------------------------------
SELECT pg_temp.expect_fail('duplicate site code in organization', $$INSERT INTO "Sites" ("Id","OrganizationId","Code","Name") VALUES (gen_random_uuid(),'11111111-1111-1111-1111-111111111111','MAIN','Dup')$$);
SELECT pg_temp.expect_fail('second primary site', $$INSERT INTO "Sites" ("Id","OrganizationId","Code","Name","IsPrimary") VALUES (gen_random_uuid(),'11111111-1111-1111-1111-111111111111','JPR','Jaipur',true)$$);
SELECT pg_temp.expect_fail('inactive primary site', $$UPDATE "Sites" SET "IsActive"=false WHERE "Id"='aaaaaaaa-0000-0000-0000-000000000001'$$);
SELECT pg_temp.expect_ok('same code in another organization', $$INSERT INTO "Sites" ("Id","OrganizationId","Code","Name") VALUES ('bbbbbbbb-0000-0000-0000-000000000002','22222222-2222-2222-2222-222222222222','DEL','Delhi')$$);
SELECT pg_temp.expect_ok('non-primary site', $$INSERT INTO "Sites" ("Id","OrganizationId","Code","Name") VALUES ('aaaaaaaa-0000-0000-0000-000000000002','11111111-1111-1111-1111-111111111111','JPR','Jaipur')$$);

-- Login methods (NULL SiteId is the tricky case for unique indexes) -----------------------------
INSERT INTO "OrganizationLoginMethods" ("Id","OrganizationId","AuthenticationType","IsDefault")
VALUES ('00000000-0000-0000-0000-0000000000b1','11111111-1111-1111-1111-111111111111','RoomMobile',true);
SELECT pg_temp.expect_fail('duplicate org-level method (NULL site)', $$INSERT INTO "OrganizationLoginMethods" ("Id","OrganizationId","AuthenticationType") VALUES (gen_random_uuid(),'11111111-1111-1111-1111-111111111111','RoomMobile')$$);
SELECT pg_temp.expect_fail('second org-level default', $$INSERT INTO "OrganizationLoginMethods" ("Id","OrganizationId","AuthenticationType","IsDefault") VALUES (gen_random_uuid(),'11111111-1111-1111-1111-111111111111','MobileOTP',true)$$);
SELECT pg_temp.expect_ok('site-level override of same method', $$INSERT INTO "OrganizationLoginMethods" ("Id","OrganizationId","SiteId","AuthenticationType","IsDefault") VALUES (gen_random_uuid(),'11111111-1111-1111-1111-111111111111','aaaaaaaa-0000-0000-0000-000000000002','RoomMobile',true)$$);
SELECT pg_temp.expect_fail('duplicate site-level method', $$INSERT INTO "OrganizationLoginMethods" ("Id","OrganizationId","SiteId","AuthenticationType") VALUES (gen_random_uuid(),'11111111-1111-1111-1111-111111111111','aaaaaaaa-0000-0000-0000-000000000002','RoomMobile')$$);

-- SMS settings ---------------------------------------------------------------------------------
SELECT pg_temp.expect_ok('org sms setting', $$INSERT INTO "OrganizationSmsSettings" ("Id","OrganizationId","ProviderName","ApiEndpoint","ApiKeyEncrypted","SenderId","MessageTemplate") VALUES (gen_random_uuid(),'11111111-1111-1111-1111-111111111111','2Factor','https://2factor.in/API/R1/','ciphertext','NXTWFI','{OTP}')$$);
SELECT pg_temp.expect_fail('second org-level sms setting', $$INSERT INTO "OrganizationSmsSettings" ("Id","OrganizationId","ProviderName","ApiEndpoint","ApiKeyEncrypted","SenderId","MessageTemplate") VALUES (gen_random_uuid(),'11111111-1111-1111-1111-111111111111','Other','https://2factor.in/API/R1/','c','NXTWFI','{OTP}')$$);
SELECT pg_temp.expect_fail('site sms override without organization', $$INSERT INTO "OrganizationSmsSettings" ("Id","SiteId","ProviderName","ApiEndpoint","ApiKeyEncrypted","SenderId","MessageTemplate") VALUES (gen_random_uuid(),'aaaaaaaa-0000-0000-0000-000000000002','2Factor','https://2factor.in/API/R1/','c','NXTWFI','{OTP}')$$);
SELECT pg_temp.expect_ok('platform default sms', $$INSERT INTO "OrganizationSmsSettings" ("Id","ProviderName","ApiEndpoint","ApiKeyEncrypted","SenderId","MessageTemplate") VALUES (gen_random_uuid(),'2Factor','https://2factor.in/API/R1/','c','NXTWFI','{OTP}')$$);
SELECT pg_temp.expect_fail('second platform default for same provider', $$INSERT INTO "OrganizationSmsSettings" ("Id","ProviderName","ApiEndpoint","ApiKeyEncrypted","SenderId","MessageTemplate") VALUES (gen_random_uuid(),'2Factor','https://2factor.in/API/R1/','c','NXTWFI','{OTP}')$$);

-- Admin users, roles ---------------------------------------------------------------------------
INSERT INTO "AdminUsers" ("Id","OrganizationId","Username","NormalizedUsername","FullName","PasswordHash")
VALUES ('cccccccc-0000-0000-0000-000000000001','11111111-1111-1111-1111-111111111111','HotelAdmin','HOTELADMIN','A','hash');
SELECT pg_temp.expect_fail('duplicate normalized username across organizations', $$INSERT INTO "AdminUsers" ("Id","OrganizationId","Username","NormalizedUsername","FullName","PasswordHash") VALUES (gen_random_uuid(),'22222222-2222-2222-2222-222222222222','hoteladmin','HOTELADMIN','B','hash')$$);

INSERT INTO "Roles" ("Id","Name","IsSystem","IsPlatformRole") VALUES ('dddddddd-0000-0000-0000-000000000001','SuperAdmin',true,true);
SELECT pg_temp.expect_fail('duplicate global role name', $$INSERT INTO "Roles" ("Id","Name") VALUES (gen_random_uuid(),'SuperAdmin')$$);
SELECT pg_temp.expect_fail('platform role bound to an organization', $$INSERT INTO "Roles" ("Id","OrganizationId","Name","IsPlatformRole") VALUES (gen_random_uuid(),'11111111-1111-1111-1111-111111111111','Evil',true)$$);
SELECT pg_temp.expect_ok('org role', $$INSERT INTO "Roles" ("Id","OrganizationId","Name") VALUES ('dddddddd-0000-0000-0000-000000000002','11111111-1111-1111-1111-111111111111','Front Desk')$$);
SELECT pg_temp.expect_fail('duplicate role name inside organization', $$INSERT INTO "Roles" ("Id","OrganizationId","Name") VALUES (gen_random_uuid(),'11111111-1111-1111-1111-111111111111','Front Desk')$$);
SELECT pg_temp.expect_ok('same role name in other organization', $$INSERT INTO "Roles" ("Id","OrganizationId","Name") VALUES (gen_random_uuid(),'22222222-2222-2222-2222-222222222222','Front Desk')$$);
INSERT INTO "AdminUserRoles" VALUES ('cccccccc-0000-0000-0000-000000000001','dddddddd-0000-0000-0000-000000000002');
SELECT pg_temp.expect_fail('delete role still assigned to a user', $$DELETE FROM "Roles" WHERE "Id"='dddddddd-0000-0000-0000-000000000002'$$);
SELECT pg_temp.expect_fail('delete organization that still has admin users', $$DELETE FROM "Organizations" WHERE "Id"='11111111-1111-1111-1111-111111111111'$$);

-- Wallet ---------------------------------------------------------------------------------------
INSERT INTO "SiteWallets" ("Id","OrganizationId","SiteId","Balance") VALUES ('eeeeeeee-0000-0000-0000-000000000001','11111111-1111-1111-1111-111111111111','aaaaaaaa-0000-0000-0000-000000000001',100);
SELECT pg_temp.expect_fail('second wallet for a site', $$INSERT INTO "SiteWallets" ("Id","OrganizationId","SiteId") VALUES (gen_random_uuid(),'11111111-1111-1111-1111-111111111111','aaaaaaaa-0000-0000-0000-000000000001')$$);
SELECT pg_temp.expect_fail('negative wallet balance', $$UPDATE "SiteWallets" SET "Balance"=-0.01 WHERE "SiteId"='aaaaaaaa-0000-0000-0000-000000000001'$$);
INSERT INTO "SiteWalletTransactions" ("Id","OrganizationId","SiteId","Type","Amount","BalanceAfter","Sequence","IdempotencyKey")
VALUES (gen_random_uuid(),'11111111-1111-1111-1111-111111111111','aaaaaaaa-0000-0000-0000-000000000001','Credit',100,100,1,'req-1');
SELECT pg_temp.expect_fail('duplicate ledger sequence', $$INSERT INTO "SiteWalletTransactions" ("Id","OrganizationId","SiteId","Type","Amount","BalanceAfter","Sequence") VALUES (gen_random_uuid(),'11111111-1111-1111-1111-111111111111','aaaaaaaa-0000-0000-0000-000000000001','Credit',1,101,1)$$);
SELECT pg_temp.expect_fail('duplicate idempotency key for a site', $$INSERT INTO "SiteWalletTransactions" ("Id","OrganizationId","SiteId","Type","Amount","BalanceAfter","Sequence","IdempotencyKey") VALUES (gen_random_uuid(),'11111111-1111-1111-1111-111111111111','aaaaaaaa-0000-0000-0000-000000000001','Credit',1,101,2,'req-1')$$);
SELECT pg_temp.expect_fail('zero amount', $$INSERT INTO "SiteWalletTransactions" ("Id","OrganizationId","SiteId","Type","Amount","BalanceAfter","Sequence") VALUES (gen_random_uuid(),'11111111-1111-1111-1111-111111111111','aaaaaaaa-0000-0000-0000-000000000001','Credit',0,100,2)$$);
SELECT pg_temp.expect_fail('invalid ledger type', $$INSERT INTO "SiteWalletTransactions" ("Id","OrganizationId","SiteId","Type","Amount","BalanceAfter","Sequence") VALUES (gen_random_uuid(),'11111111-1111-1111-1111-111111111111','aaaaaaaa-0000-0000-0000-000000000001','Refund',1,100,2)$$);
SELECT pg_temp.expect_ok('two ledger rows without idempotency key', $$INSERT INTO "SiteWalletTransactions" ("Id","OrganizationId","SiteId","Type","Amount","BalanceAfter","Sequence") VALUES (gen_random_uuid(),'11111111-1111-1111-1111-111111111111','aaaaaaaa-0000-0000-0000-000000000001','Credit',1,101,2), (gen_random_uuid(),'11111111-1111-1111-1111-111111111111','aaaaaaaa-0000-0000-0000-000000000001','Debit',1,100,3)$$);
SELECT pg_temp.expect_fail('delete site that has ledger history', $$DELETE FROM "Sites" WHERE "Id"='aaaaaaaa-0000-0000-0000-000000000001'$$);

-- Audit trail outlives the rows it describes ---------------------------------------------------
INSERT INTO "AuditLogs" ("Id","OrganizationId","AdminUserId","Action") VALUES (gen_random_uuid(),'33333333-3333-3333-3333-333333333333',gen_random_uuid(),'OrganizationRegistered');
SELECT pg_temp.expect_ok('audit entry for an id with no matching row', $$SELECT 1$$);

DO $$ BEGIN RAISE NOTICE 'ALL CONSTRAINT TESTS PASSED'; END $$;
