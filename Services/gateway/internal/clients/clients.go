package clients

import (
	"fmt"

	"github.com/danetka/gateway/internal/config"
	authpb "github.com/danetka/gateway/pkg/grpc/auth"
	parserpb "github.com/danetka/gateway/pkg/grpc/parser"
	puzzlepb "github.com/danetka/gateway/pkg/grpc/puzzle"
	"google.golang.org/grpc"
	"google.golang.org/grpc/credentials/insecure"
)

type Clients struct {
	Auth   authpb.AuthServiceClient
	Puzzle puzzlepb.PuzzleServiceClient
	Parser parserpb.ParserServiceClient

	conns []*grpc.ClientConn
}

func New(cfg config.Config) (*Clients, error) {
	authConn, err := dial(cfg.AuthServiceAddr)
	if err != nil {
		return nil, fmt.Errorf("auth service dial: %w", err)
	}

	puzzleConn, err := dial(cfg.PuzzleServiceAddr)
	if err != nil {
		_ = authConn.Close()
		return nil, fmt.Errorf("puzzle service dial: %w", err)
	}

	parserConn, err := dial(cfg.ParserServiceAddr)
	if err != nil {
		_ = authConn.Close()
		_ = puzzleConn.Close()
		return nil, fmt.Errorf("parser service dial: %w", err)
	}

	return &Clients{
		Auth:   authpb.NewAuthServiceClient(authConn),
		Puzzle: puzzlepb.NewPuzzleServiceClient(puzzleConn),
		Parser: parserpb.NewParserServiceClient(parserConn),
		conns:  []*grpc.ClientConn{authConn, puzzleConn, parserConn},
	}, nil
}

func (c *Clients) Close() error {
	var firstErr error
	for _, conn := range c.conns {
		if err := conn.Close(); err != nil && firstErr == nil {
			firstErr = err
		}
	}
	return firstErr
}

func dial(address string) (*grpc.ClientConn, error) {
	return grpc.NewClient(
		address,
		grpc.WithTransportCredentials(insecure.NewCredentials()),
	)
}
