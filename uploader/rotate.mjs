// One rotation run: take the last VSSetting.byaml, generate a new schedule with Rotationator,
// upload it to nn-boss for every Splatoon BOSS app id (the same call as
// `boss file create <app> schdat2 --name VSSetting.byaml --type AppData --notify-new app`).
import { execFileSync } from 'node:child_process';
import { existsSync, readFileSync, writeFileSync, renameSync, mkdirSync } from 'node:fs';
import { request } from 'undici';
import { createChannel, createClient, Metadata } from 'nice-grpc';
import { BossServiceDefinition } from '@pretendonetwork/grpc/boss/v2/boss_service';
import { decryptWiiU } from '@pretendonetwork/boss-crypto';

const env = (k, d) => process.env[k]?.trim() || d;

const grpcHost = env('ROTATIONATOR_GRPC_HOST', 'openpak-nn-boss:20081');
const apiKey = env('ROTATIONATOR_GRPC_APIKEY', env('PN_BOSS_CONFIG_GRPC_BOSS_SERVER_API_KEY'));
if (!apiKey) { console.error('missing ROTATIONATOR_GRPC_APIKEY'); process.exit(2); }
const apps = env('ROTATIONATOR_APP_IDS', 'bb6tOEckvgZ50ciH,rjVlM7hUXPxmYQJh,zvGSM4kOrXpkKnpT').split(',');
const taskId = env('ROTATIONATOR_TASK_ID', 'schdat2');
const npdiUrl = env('ROTATIONATOR_NPDI_URL', 'http://openpak-nn-boss:20080');
const npdiHost = env('ROTATIONATOR_NPDI_HOST', 'npdi.cdn.openpak.org');
const dataDir = env('ROTATIONATOR_DATA_DIR', '/data');
const dryRun = env('ROTATIONATOR_DRY_RUN', '') === '1';
const last = `${dataDir}/last.byaml`;
const out = `${dataDir}/next.byaml`;
mkdirSync(dataDir, { recursive: true });

const grpc = createClient(BossServiceDefinition, createChannel(grpcHost), {
	'*': { metadata: new Metadata({ 'X-API-Key': apiKey }) }
});

// The previous schedule: our own last output if we have one, otherwise the newest
// VSSetting.byaml nn-boss serves for the first app (it is encrypted, so this needs the BOSS keys once).
if (!existsSync(last)) {
	const aes = env('ROTATIONATOR_WIIU_AES_KEY', env('PN_BOSS_CONFIG_BOSS_WIIU_AES_KEY'));
	const hmac = env('ROTATIONATOR_WIIU_HMAC_KEY', env('PN_BOSS_CONFIG_BOSS_WIIU_HMAC_KEY'));
	if (!aes || !hmac) {
		console.error(`no ${last} and no ROTATIONATOR_WIIU_AES_KEY/HMAC_KEY to decrypt the served file. ` +
			`Put a plaintext VSSetting.byaml at ${last}, or supply the console-dumped BOSS keys.`);
		process.exit(3);
	}
	const { files } = await grpc.listFilesWUP({ bossAppId: apps[0], taskId, any: true });
	const f = files.filter(x => x.name === 'VSSetting.byaml').sort((a, b) => Number(b.dataId - a.dataId))[0];
	if (!f) { console.error('nn-boss has no VSSetting.byaml to start from'); process.exit(3); }
	const r = await request(`${npdiUrl}/p01/data/1/${f.bossAppId}/${f.dataId}/${f.hash}`, { headers: { Host: npdiHost } });
	if (r.statusCode > 299) { console.error(`download failed: ${r.statusCode}`); process.exit(3); }
	const enc = Buffer.from(await r.body.arrayBuffer());
	writeFileSync(last, decryptWiiU(enc, aes, hmac).content);
	console.log(`seeded ${last} from data ID ${f.dataId}`);
}

execFileSync('dotnet', ['/app/rotationator/Rotationator.dll', 'generate', last, out,
	'--phaseLength', env('ROTATIONATOR_PHASE_HOURS', '4'),
	'--scheduleLength', env('ROTATIONATOR_SCHEDULE_DAYS', '30'),
	...(existsSync(`${dataDir}/overrides.json`) ? ['--overridePhases', `${dataDir}/overrides.json`] : [])
], { stdio: 'inherit' });

if (dryRun) { console.log(`dry run: generated ${out}, not uploading`); process.exit(0); }

const data = readFileSync(out);
for (const app of apps) {
	const { file } = await grpc.uploadFileWUP({
		taskId, bossAppId: app, name: 'VSSetting.byaml', type: 'AppData',
		supportedCountries: [], supportedLanguages: [], attributes: {},
		nameEqualsDataId: false, data, notifyOnNew: ['app'], notifyLed: false
	});
	if (!file) { console.error(`upload to ${app} failed`); process.exit(4); }
	console.log(`${app}: created file with data ID ${file.dataId}`);
}
renameSync(out, last);
// The JSON twin of the schedule, for the Splatoon game server: it reads this file (the
// openpak-rotationator-data volume, read-only) to close lobbies at each phase boundary.
// Written only after a successful upload, so it never runs ahead of what consoles download.
if (existsSync(`${out}.json`)) {
	renameSync(`${out}.json`, `${dataDir}/schedule.json`);
	console.log(`wrote ${dataDir}/schedule.json`);
}
