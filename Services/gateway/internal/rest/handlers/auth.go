package handlers

import (
	"net/http"

	"github.com/danetka/gateway/internal/clients"
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

func RegisterAuthRoutes(group *gin.RouterGroup, grpcClients *clients.Clients) {
	handler := NewAuthHandler(grpcClients)
	group.POST("/register", handler.Register)
	group.POST("/login", handler.Login)
}
