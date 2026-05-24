# --- ЭТАП СБОРКИ ---
    FROM node:20-alpine AS build
    WORKDIR /app
    
    # Копируем файлы конфигурации npm
    COPY package*.json ./
    RUN npm ci
    
    # Копируем исходный код и собираем продакшн-билд
    COPY . .
    RUN npm run build
    
    # --- ЭТАП ЗАПУСКА (PRODUCTION) ---
    FROM nginx:stable-alpine
    
    # Копируем собранную статику из предыдущего этапа в папку Nginx
    COPY --from=build /app/dist /usr/share/nginx/html
    
    # Копируем кастомный конфиг Nginx для поддержки React Router
    COPY nginx.conf /etc/nginx/conf.d/default.conf
    
    EXPOSE 80
    CMD ["nginx", "-g", "daemon off;"]