package middleware

import (
	"net/http"
	"strings"

	"github.com/danetka/gateway/internal/clients"
	"github.com/danetka/gateway/internal/rest/handlers"
	authpb "github.com/danetka/gateway/pkg/grpc/auth"
	"github.com/gin-gonic/gin"
)

const (
	ContextUserIDKey  = "user_id"
	ContextUserRoleKey = "role"
	ContextUserEmailKey = "email"

	adminRoleName = "Admin"
)

func Auth(grpcClients *clients.Clients) gin.HandlerFunc {
	return func(c *gin.Context) {
		token := extractBearerToken(c.GetHeader("Authorization"))
		if token == "" {
			c.AbortWithStatusJSON(http.StatusUnauthorized, handlers.ErrorResponse{Error: "authorization token is required"})
			return
		}

		response, err := grpcClients.Auth.ValidateToken(c.Request.Context(), &authpb.ValidateTokenRequest{
			Token: token,
		})
		if err != nil {
			handlers.WriteGRPCError(c, err)
			c.Abort()
			return
		}

		if !response.Valid {
			c.AbortWithStatusJSON(http.StatusUnauthorized, handlers.ErrorResponse{Error: "invalid or expired token"})
			return
		}

		c.Set(ContextUserIDKey, response.UserId)
		c.Set(ContextUserRoleKey, response.Role)
		c.Set(ContextUserEmailKey, response.Email)
		c.Next()
	}
}

func RequireRole(requiredRole string) gin.HandlerFunc {
	return func(c *gin.Context) {
		roleValue, exists := c.Get(ContextUserRoleKey)
		if !exists {
			c.AbortWithStatusJSON(http.StatusUnauthorized, handlers.ErrorResponse{Error: "user role is missing"})
			return
		}

		role, ok := roleValue.(string)
		if !ok || !strings.EqualFold(role, requiredRole) {
			c.AbortWithStatusJSON(http.StatusForbidden, handlers.ErrorResponse{Error: "insufficient permissions"})
			return
		}

		c.Next()
	}
}

func RequireAdmin() gin.HandlerFunc {
	return RequireRole(adminRoleName)
}

func extractBearerToken(header string) string {
	if header == "" {
		return ""
	}

	const prefix = "Bearer "
	if !strings.HasPrefix(header, prefix) {
		return ""
	}

	return strings.TrimSpace(strings.TrimPrefix(header, prefix))
}
