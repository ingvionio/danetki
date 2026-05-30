VPS ?= root@194.67.116.198
REMOTE ?= /opt/danetka
RSYNC_EXCLUDE := --exclude frontend/node_modules --exclude .git --exclude '**/bin' --exclude '**/obj'

.PHONY: sync deploy deploy-app deploy-frontend deploy-gateway deploy-puzzle deploy-ai deploy-parser frontend-fast up reload-nginx

reload-nginx:
	ssh $(VPS) 'cd $(REMOTE) && docker compose exec -T nginx nginx -s reload 2>/dev/null || docker compose restart nginx'

sync:
	rsync -az $(RSYNC_EXCLUDE) ./ $(VPS):$(REMOTE)/

SVC ?=
deploy: sync
	ssh $(VPS) 'cd $(REMOTE) && docker compose build $(SVC) && docker compose up -d --no-build $(SVC) && (echo $(SVC) | grep -qE "frontend|gateway" && docker compose exec -T nginx nginx -s reload 2>/dev/null || true)'

deploy-app: sync
	ssh $(VPS) 'cd $(REMOTE) && docker compose build gateway frontend puzzle-service ai-worker auth-service && docker compose up -d --no-build gateway frontend puzzle-service ai-worker auth-service && docker compose up -d --no-build --scale gateway=2 gateway && docker compose exec -T nginx nginx -s reload'

deploy-frontend: sync
	ssh $(VPS) 'cd $(REMOTE) && docker compose build frontend && docker compose up -d --no-build frontend && docker compose exec -T nginx nginx -s reload'

deploy-gateway: sync
	ssh $(VPS) 'cd $(REMOTE) && docker compose build gateway && docker compose up -d --no-build --scale gateway=2 gateway && docker compose exec -T nginx nginx -s reload'

deploy-puzzle: sync
	ssh $(VPS) 'cd $(REMOTE) && docker compose build puzzle-service && docker compose up -d --no-build puzzle-service'

deploy-ai: sync
	ssh $(VPS) 'cd $(REMOTE) && docker compose build ai-worker && docker compose up -d --no-build ai-worker'

deploy-parser: sync
	ssh $(VPS) 'cd $(REMOTE) && docker compose build parser-service && docker compose up -d --no-build parser-service'

frontend-fast:
	cd frontend && npm run build
	rsync -az frontend/dist/ $(VPS):$(REMOTE)/frontend/dist/
	ssh $(VPS) 'docker cp $(REMOTE)/frontend/dist/. danetka-frontend:/usr/share/nginx/html/ && docker compose -f $(REMOTE)/docker-compose.yml exec -T nginx nginx -s reload 2>/dev/null || true'

up:
	ssh $(VPS) 'cd $(REMOTE) && docker compose up -d --scale gateway=2'
