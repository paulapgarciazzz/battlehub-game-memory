# Se construye desde esta carpeta (docker-compose.yml de la raíz).

FROM node:22-alpine AS build
WORKDIR /app

# Primero solo las dependencias: el npm ci queda en caché mientras no cambien.
COPY package.json package-lock.json ./
RUN npm ci

COPY . .

# environment.production.json viene sin direcciones; se completan aquí con la
# de la API, sin tocar el archivo del repo.
ARG MEMORY_API_URL=http://localhost:5095
RUN node -e "const fs = require('fs'); const f = 'config/environment.production.json'; const c = JSON.parse(fs.readFileSync(f, 'utf8')); c.apiBaseUrl = c.hubBaseUrl = process.env.MEMORY_API_URL; fs.writeFileSync(f, JSON.stringify(c, null, 2));"
RUN npm run build

FROM nginx:1.27-alpine
COPY nginx.conf /etc/nginx/conf.d/default.conf
COPY --from=build /app/dist /usr/share/nginx/html
