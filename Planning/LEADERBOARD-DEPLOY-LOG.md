# Leaderboard service deploy log (2026-11-12)

Owner approved: deploy now, public Cloud Run endpoint (service checks Firebase ID tokens itself), max 5 instances. The app feature flag stays OFF. Account jpdash.creator@gmail.com, project fluffy-climbers-tower-jump (763742589585), region asia-south1. Every gcloud call used `--project=fluffy-climbers-tower-jump --account=jpdash.creator@gmail.com` (shown as $A); no `gcloud config set`.

1. Local checks: `dotnet build Tools/leaderboard_service --artifacts-path <scratch>`; `FLUFFY_ROOT=E:\Code\fluffy-climbers-sky-tower dotnet test Tests/LeaderboardService --artifacts-path <scratch>`: 30 passed, 0 failed.
2. Build context staged in a scratch folder: `Tools/leaderboard_service` (no bin/obj), the `*.cs` of `Assets/_Game/{Simulation,Content,Economy}` (77 files, same relative paths), `Assets/Resources/Content/content_manifest.json`, and this Dockerfile (no secrets, keystores or google-services.json):
```
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY . .
RUN dotnet publish Tools/leaderboard_service/LeaderboardService.csproj -c Release -o /app
FROM mcr.microsoft.com/dotnet/aspnet:10.0
WORKDIR /app
COPY --from=build /app .
ENV ASPNETCORE_ENVIRONMENT=Production
ENTRYPOINT ["dotnet","FluffyClimbers.LeaderboardService.dll"]
```
3. `gcloud services enable secretmanager.googleapis.com $A` (only new API; Cloud Run source deploy also created the Artifact Registry repo `cloud-run-source-deploy`).
4. Secret: 32 random bytes from python `secrets.token_urlsafe(32)` written to a temp file, then `gcloud secrets create lb-pid-salt --replication-policy=user-managed --locations=asia-south1 --data-file=<temp> $A`; temp file deleted. Value never printed.
5. Runtime identity:
```
gcloud iam service-accounts create fluffy-leaderboard-run --display-name="Fluffy leaderboard Cloud Run runtime" $A
gcloud projects add-iam-policy-binding fluffy-climbers-tower-jump --member=serviceAccount:fluffy-leaderboard-run@fluffy-climbers-tower-jump.iam.gserviceaccount.com --role=roles/datastore.user --condition=None $A
gcloud secrets add-iam-policy-binding lb-pid-salt --member=serviceAccount:fluffy-leaderboard-run@fluffy-climbers-tower-jump.iam.gserviceaccount.com --role=roles/secretmanager.secretAccessor $A
```
6. Deploy (from the staged folder):
```
gcloud run deploy fluffy-leaderboard --source . --region=asia-south1 --max-instances=5 --min-instances=0 --cpu=1 --memory=512Mi --timeout=60 --concurrency=40 --allow-unauthenticated --service-account=fluffy-leaderboard-run@fluffy-climbers-tower-jump.iam.gserviceaccount.com --set-env-vars=FIREBASE_PROJECT=fluffy-climbers-tower-jump --set-secrets=LB_PID_SALT=lb-pid-salt:latest $A --quiet
```
Result: revision `fluffy-leaderboard-00001-zhn`, 100 percent traffic, URL https://fluffy-leaderboard-763742589585.asia-south1.run.app
7. Smoke (curl, no Firestore writes): `GET /healthz/` 200 `{"ok":true,...}` (plain `/healthz` returns Google's 404 page: the run.app front end reserves it, use the trailing slash); `POST /start-run` no token 401; with a fake Bearer 401; `GET /my-week` 401; `POST /admin/tick` 401 (LB_ADMIN_KEY unset).

Not done: Cloud Scheduler tick (needs LB_ADMIN_KEY secret), `config/leaderboard` seeding (defaults come from config.default.json), real-token test, Firebase Auth Play Games provider, client work.
Redeploy after ANY simulation, generator, hero table or manifest change (same commit as the release).
