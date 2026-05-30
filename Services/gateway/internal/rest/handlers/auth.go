package handlers

import (
	"net/http"

	"github.com/danetka/gateway/internal/clients"
	"github.com/danetka/gateway/internal/rest/middleware"
	authpb "github.com/danetka/gateway/pkg/grpc/auth"
	"github.com/gin-gonic/gin"
)

type AuthHandler struct {
	clients *clients.Clients
}

func NewAuthHandler(grpcClients *clients.Clients) *AuthHandler {
	return &AuthHandler{clients: grpcClients}
}

type registerRequest struct {
	Email    string `json:"email" binding:"required"`
	Password string `json:"password" binding:"required"`
	Username string `json:"username" binding:"required"`
}

type loginRequest struct {
	Email    string `json:"email" binding:"required"`
	Password string `json:"password" binding:"required"`
}

type addTokensRequest struct {
	Amount int32 `json:"amount" binding:"required"`
}

func (h *AuthHandler) Register(c *gin.Context) {
	var body registerRequest
	if err := c.ShouldBindJSON(&body); err != nil {
		c.JSON(http.StatusBadRequest, ErrorResponse{Error: "invalid request body"})
		return
	}

	response, err := h.clients.Auth.Register(c.Request.Context(), &authpb.RegisterRequest{
		Email:    body.Email,
		Password: body.Password,
		Username: body.Username,
	})
	if err != nil {
		WriteGRPCError(c, err)
		return
	}

	c.JSON(http.StatusCreated, gin.H{
		"user_id":    response.UserId,
		"token":      response.Token,
		"expires_at": response.ExpiresAt,
	})
}

func (h *AuthHandler) Login(c *gin.Context) {
	var body loginRequest
	if err := c.ShouldBindJSON(&body); err != nil {
		c.JSON(http.StatusBadRequest, ErrorResponse{Error: "invalid request body"})
		return
	}

	response, err := h.clients.Auth.Login(c.Request.Context(), &authpb.LoginRequest{
		Email:    body.Email,
		Password: body.Password,
	})
	if err != nil {
		WriteGRPCError(c, err)
		return
	}

	c.JSON(http.StatusOK, gin.H{
		"user_id":    response.UserId,
		"token":      response.Token,
		"expires_at": response.ExpiresAt,
	})
}

func (h *AuthHandler) GetMe(c *gin.Context) {
	userID, ok := c.Get(middleware.ContextUserIDKey)
	if !ok {
		c.JSON(http.StatusUnauthorized, ErrorResponse{Error: "user id is missing"})
		return
	}

	userIDValue, ok := userID.(string)
	if !ok || userIDValue == "" {
		c.JSON(http.StatusUnauthorized, ErrorResponse{Error: "invalid user id"})
		return
	}

	response, err := h.clients.Auth.GetUser(c.Request.Context(), &authpb.GetUserRequest{
		UserId: userIDValue,
	})
	if err != nil {
		WriteGRPCError(c, err)
		return
	}

	c.JSON(http.StatusOK, userToJSON(response))
}

func (h *AuthHandler) ConsumeToken(c *gin.Context) {
	userID, ok := c.Get(middleware.ContextUserIDKey)
	if !ok {
		c.JSON(http.StatusUnauthorized, ErrorResponse{Error: "user id is missing"})
		return
	}

	userIDValue, ok := userID.(string)
	if !ok || userIDValue == "" {
		c.JSON(http.StatusUnauthorized, ErrorResponse{Error: "invalid user id"})
		return
	}

	response, err := h.clients.Auth.ConsumeToken(c.Request.Context(), &authpb.ConsumeTokenRequest{
		UserId: userIDValue,
	})
	if err != nil {
		WriteGRPCError(c, err)
		return
	}

	c.JSON(http.StatusOK, gin.H{
		"success":           response.Success,
		"remaining_tokens": response.RemainingTokens,
	})
}

func (h *AuthHandler) ListUsers(c *gin.Context) {
	response, err := h.clients.Auth.ListUsers(c.Request.Context(), &authpb.ListUsersRequest{})
	if err != nil {
		WriteGRPCError(c, err)
		return
	}

	users := make([]gin.H, 0, len(response.Users))
	for _, user := range response.Users {
		users = append(users, userToJSON(user))
	}

	c.JSON(http.StatusOK, gin.H{"users": users})
}

func (h *AuthHandler) AddTokens(c *gin.Context) {
	userID := c.Param("id")
	if userID == "" {
		c.JSON(http.StatusBadRequest, ErrorResponse{Error: "user id is required"})
		return
	}

	var body addTokensRequest
	if err := c.ShouldBindJSON(&body); err != nil {
		c.JSON(http.StatusBadRequest, ErrorResponse{Error: "invalid request body"})
		return
	}

	response, err := h.clients.Auth.AddTokens(c.Request.Context(), &authpb.AddTokensRequest{
		UserId: userID,
		Amount: body.Amount,
	})
	if err != nil {
		WriteGRPCError(c, err)
		return
	}

	c.JSON(http.StatusOK, userToJSON(response))
}

func RegisterAuthRoutes(group *gin.RouterGroup, grpcClients *clients.Clients) {
	handler := NewAuthHandler(grpcClients)
	group.POST("/register", handler.Register)
	group.POST("/login", handler.Login)
}

func RegisterProtectedAuthRoutes(group *gin.RouterGroup, grpcClients *clients.Clients) {
	handler := NewAuthHandler(grpcClients)
	group.GET("/me", handler.GetMe)
	group.POST("/me/consume", handler.ConsumeToken)
}

func RegisterAdminUserRoutes(group *gin.RouterGroup, grpcClients *clients.Clients) {
	handler := NewAuthHandler(grpcClients)
	group.GET("", handler.ListUsers)
	group.POST("/:id/tokens", handler.AddTokens)
}

func userToJSON(user *authpb.UserResponse) gin.H {
	return gin.H{
		"user_id":           user.UserId,
		"email":             user.Email,
		"username":          user.Username,
		"created_at":        user.CreatedAt,
		"role":              protoRoleToString(user.Role),
		"tokens":            user.Tokens,
		"subscription_plan": user.SubscriptionPlan,
	}
}

func protoRoleToString(role authpb.Role) string {
	if role == authpb.Role_ADMIN {
		return "Admin"
	}

	return "User"
}
