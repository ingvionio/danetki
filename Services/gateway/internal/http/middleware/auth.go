package middleware

import (
	"context"
	"net/http"
	"strings"
	"time"

	"github.com/ingvionio/danetki/internal/contracts/auth"
	"github.com/ingvionio/danetki/internal/discovery"

	"google.golang.org/grpc"
	"google.golang.org/grpc/credentials/insecure"
)

func AuthMiddleware(reg discovery.Registry) func(http.Handler) http.Handler {
	return func(next http.Handler) http.Handler {
		return http.HandlerFunc(func(w http.ResponseWriter, r *http.Request) {
			if strings.HasPrefix(r.URL.Path, "/auth/register") || strings.HasPrefix(r.URL.Path, "/auth/login") {
				next.ServeHTTP(w, r)
				return
			}

			authHeader := r.Header.Get("Authorization")
			if authHeader == "" || !strings.HasPrefix(authHeader, "Bearer ") {
				http.Error(w, "Unauthorized: Missing token", http.StatusUnauthorized)
				return
			}

			token := strings.TrimPrefix(authHeader, "Bearer ")

			addr := "danetka-auth:8080"

			conn, error := grpc.Dial(addr, grpc.WithTransportCredentials(insecure.NewCredentials()))
			if error != nil {
				http.Error(w, "Failed to connect to auth service", http.StatusInternalServerError)
				return
			}
			defer conn.Close()

			client := auth.NewAuthServiceClient(conn)
			ctx, cancel := context.WithTimeout(context.Background(), time.Second*5)
			defer cancel()

			resp, error := client.ValidateToken(ctx, &auth.ValidateTokenRequest{Token: token})
			if error != nil || !resp.Valid {
				http.Error(w, "Unauthorized: Invalid token", http.StatusUnauthorized)
				return
			}

			ctx = context.WithValue(r.Context(), "user_id", resp.UserId)
			ctx = context.WithValue(ctx, "email", resp.Email)

			next.ServeHTTP(w, r.WithContext(ctx))
		})
	}
}