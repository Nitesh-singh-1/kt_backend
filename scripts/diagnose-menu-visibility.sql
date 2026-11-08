Sorting andfrying,noadded preservativesoradditives,soit's great forsorting andeveryday Indian cookinggrowth.<en-US> Are you tomaybecoffee Japaneseneighborsnever completesoI justreceiveda call fromadministrative office ofmysociety and they wereaskingme aboutchildren,scan yourhandmarriage at twenty that's whatIused to thinkI'm justaguy,myhourshow perchant,plurallymyhogic,oldinsight,nightsonlyhestoppedsharing things onedayI askedhimwhyhe said,I got promoted three monthsago.<en-US> Nobody knowsIassuredwhy didn't you tell anyone his mindshowedmeanold chat whenhe hadshared hisphosphonotiononfront of typemust benice some ofusarestillstruggling and others said okayRichardSnow someoneelse ischanging upit saidIrealize people were comfortable problems butmyhappiness somehowmayinflict then youwon't bea part and thenhappensI celebrate quite and whenever somethingbadhappensIdo not quitedoingso afraid ofmaking theland comfortable that youstopletting them know you werehappynevermeet someonefeelsmall forsharingalittlejoint how many times theyare to celebrate themselves quisiersharedrivesstuffintojoiningstudentsaior cloudmesheltesenvironment milta iscookedmemberscomplets workshopchoose carset properly follow carroton the explaininglearningorsevere completelink and commentasnewkonce workshop tray andlots ofmodsstrong the familyvaluestronghotel becausereality comes from goodabringing andstrongbloodline and betrayal comes from thecharacter dirtyhearts andlack ofhonorbasicsetsworth takingspiritfrictioncooky aboutseriouscopy correcthoneyshirtvaps applicantcoping numberstuffmatter anywaysannoying things people sayinmy comments is okay nowshow theriot becausecurrentlyI'm working atmarinedrive andas you can see behindmeit is clean there was evenpublic toilet thatI justwalkedin andwalked out and then they're likeoh but you'reinsouthhomeareal Indianhalf Marine Drive is actually one of themostshowbasesin Indianmovies.<en-US>Like this is one of themostfamousplaceswhere yougo to New York you alsogo to like TimesSquare by thatlogic TimesSquare is therealyard timessquare can countas like therealUS but Marine Drive isn't howdoes this makesense?<en-US>It's one of themostensuring thingsinmycollege.<en-US> Weshow therural India.<en-US> Hi,it's theRome India.<en-US>Likeit'sgame.<en-US> Yes, India hasawish management problem, but thereare alsoalot of reallynice teamplaces like this one.<en-US> And thereasonwhyI'mshowingit is because peopleareinourshortshowers excuseme, that'sawonderfulleather jacket that you're wearing.<en-US>Iabsolutelyloveit.<en-US> Ohno, youarewonderful too.<en-US>Imean you couldfind that out you want togograbacoffee withmelaterI gota question for you if you don't minddo you think looksareimportantinarelationshipit'simportant toan extent okayIagree with thatagree with thatI've gota quote aboutitabsolutelyloveit if you canrecord this video formeit'sallrecording justkind ofhold and looking foragirlfriend anyaccidenthotelstreetgoingonplatform travels thirdnoshortphessa,huh?<en-US> Manage togardefinancereal estateinsosimple-- ============================================================================
-- diagnose-menu-visibility.sql  (READ-ONLY — runs no UPDATE/DELETE)
--
-- Purpose: pinpoint, for ONE tenant, why a module assigned by the platform
-- admin does not appear in the tenant admin's sidebar. Run this against the
-- live Postgres DB and paste the output back.
--
-- Usage (psql):
--   \set tenant '00000000-0000-0000-0000-000000000000'
--   \i diagnose-menu-visibility.sql
-- or replace :'tenant' below with the literal tenant uuid.
--
-- Each section prints a heading row so the result is easy to read top-down.
-- ============================================================================

\echo '==== [1] Enabled tenant_modules (what the tenant is allowed to see) ===='
SELECT m.code, m.name, tm.is_enabled, tm.enabled_from, tm.enabled_until
FROM tenant_modules tm
JOIN modules m ON m.id = tm.module_id
WHERE tm.tenant_id = :'tenant'
ORDER BY (tm.enabled_until IS NULL) DESC, m.code;

\echo '==== [2] Admin role row(s) for this tenant (duplicates = red flag) ===='
SELECT id, code, name, is_system, is_active
FROM roles
WHERE tenant_id = :'tenant' AND lower(code) = 'admin';

\echo '==== [3] Admin-class users and their ACTIVE role assignments ===='
\echo '      (an admin user with NO row here = the keystone bug cannot self-heal)'
SELECT u.id AS user_id, u.username, u.role AS user_role_string,
       ur.role_id AS assigned_role_id, r.code AS assigned_role_code,
       ur.revoked_at
FROM users u
LEFT JOIN user_roles ur ON ur.user_id = u.id AND ur.tenant_id = u.tenant_id AND ur.revoked_at IS NULL
LEFT JOIN roles r ON r.id = ur.role_id
WHERE u.tenant_id = :'tenant'
  AND lower(u.role) IN ('admin','superadmin','tenantadmin','super_user','tenant_owner')
ORDER BY u.username;

\echo '==== [4] Active admin role_permissions count (stale/missing = old failure mode) ===='
SELECT r.id AS role_id, count(rp.*) AS active_grants
FROM roles r
LEFT JOIN role_permissions rp ON rp.role_id = r.id AND rp.revoked_at IS NULL
WHERE r.tenant_id = :'tenant' AND lower(r.code) = 'admin'
GROUP BY r.id;

\echo '==== [5] CATALOG WIRING GAPS — menu_items whose permission_key has no permission row ===='
\echo '      (any row here = that menu can NEVER show for anyone — a wiring bug)'
SELECT mi.key, mi.parent_key, mi.title, mi.permission_key, mi.module_id, mi.path
FROM menu_items mi
WHERE mi.is_active = true
  AND mi.permission_key IS NOT NULL
  AND mi.permission_key NOT LIKE '%.module'          -- parent-group placeholders are exempt
  AND NOT EXISTS (SELECT 1 FROM permissions p WHERE p.key = mi.permission_key)
  AND NOT EXISTS (SELECT 1 FROM permissions p WHERE p.feature_key = mi.permission_key)
ORDER BY mi.parent_key NULLS FIRST, mi.display_order;

\echo '==== [6] Permissions with NULL/0 module_id (orphans the module gate) ===='
SELECT key, feature_key, module_id
FROM permissions
WHERE module_id IS NULL OR module_id = 0
ORDER BY feature_key;

\echo '==== [7] menu_items whose module_id is NOT an enabled tenant_module for THIS tenant ===='
\echo '      (these are the menus that will be hidden for the tenant admin, by design or by gap)'
SELECT m.code AS module_code,
       (SELECT count(*) FROM menu_items mi WHERE mi.module_id = m.id AND mi.is_active) AS menu_rows,
       EXISTS (SELECT 1 FROM tenant_modules tm
               WHERE tm.tenant_id = :'tenant' AND tm.module_id = m.id AND tm.enabled_until IS NULL) AS module_enabled_for_tenant
FROM modules m
WHERE m.is_active
ORDER BY module_enabled_for_tenant DESC, m.code;
