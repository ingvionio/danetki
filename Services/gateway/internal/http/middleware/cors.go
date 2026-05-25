package middleware

import "net/http"

// CORSMiddleware обрабатывает предварительные запросы OPTIONS и добавляет заголовки CORS
func CORSMiddleware(next http.Handler) http.Handler {
	return http.HandlerFunc(func(w http.ResponseWriter, r *http.Request) {
		// Разрешаем запросы с нашего локального фронтенда
		w.Header().Set("Access-Control-Allow-Origin", "http://localhost:3000")
		
		// Разрешаем методы, которые использует Axios на фронтенде
		w.Header().Set("Access-Control-Allow-Methods", "GET, POST, PUT, DELETE, OPTIONS")
		
		// Обязательно разрешаем заголовок Authorization, чтобы шлюз читал JWT-токены
		w.Header().Set("Access-Control-Allow-Headers", "Content-Type, Authorization, Accept")
		
		// Разрешаем отправку кук и учетных данных при необходимости
		w.Header().Set("Access-Control-Allow-Credentials", "true")

		// Если браузер прислал preflight-запрос OPTIONS, мгновенно возвращаем 200 OK
		if r.Method == "OPTIONS" {
			w.WriteHeader(http.StatusOK)
			return
		}

		// Обычные запросы (POST, GET) передаем дальше по цепочке роутера
		next.ServeHTTP(w, r)
	})
}