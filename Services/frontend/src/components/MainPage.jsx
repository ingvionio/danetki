import React, { useState, useEffect} from 'react';
import { useParams, useNavigate } from 'react-router-dom';
import api from '../api/client';
import PuzzleCard from './PuzzleCard';
import { Card, Button, Spin } from 'antd';

function MainPage({ onLogout }) {
  const { id } = useParams();
  const navigate = useNavigate();

  const [puzzle, setPuzzle] = useState(null);
  const [error, setError] = useState('');
  const [loading, setLoading] = useState(false);

  const handleLogout = () => {
    navigate('/', { replace: true });
    onLogout();
  };

  const fetchRandomPuzzle = async () => {
    setError('');
    try {
      const response = await api.get('/puzzle/random');
      setPuzzle(response.data);
    } catch (err) {
      setError(`Ошибка при получении пазла: ${err.response?.data || err.message}`);
    }
  };

  const fetchPuzzleById = async (puzzleId) => {
    setLoading(true);
    try {
      const response = await api.get(`/puzzle/${puzzleId}`);
      setPuzzle(response.data);
    } catch (error) {
      console.error(error);
      message.error(
        `Не удалось загрузить данетку: ${error.response?.data?.desc || error.message}`
      );
      navigate('/');
    } finally {
      setLoading(false);
    }
  };

  useEffect(() => {
    if (id) {
      fetchPuzzleById(id);
    } else {
      setPuzzle(null);
    }
  }, [id]);

  return (
    <div style={{ padding: '24px', maxWidth: '800px', margin: '0 auto' }}>
      <Card title="Игра Данетки" style={{ textAlign: 'center' }}>
        <div style={{ margin: '20px 0' }}>
          <Button 
            type="primary" 
            onClick={fetchRandomPuzzle} 
            loading={loading}
            style={{ marginBottom: '20px' }}
          >
            Получить случайную данетку
          </Button>

          <Button 
            danger 
            onClick={handleLogout}
          >
            Выйти
          </Button>
        </div>

        {loading && <div style={{ margin: '20px 0' }}><Spin size="large" /></div>}

        {/* Использование нашего чистого компонента */}
        <PuzzleCard puzzle={puzzle} loading={loading} />
        
        </Card>
      </div>
  );
}

export default MainPage;