import React, { useState } from 'react';
import api from '../api/client';

function MainPage({ onLogout }) {
  const [puzzle, setPuzzle] = useState(null);
  const [error, setError] = useState('');

  const fetchRandomPuzzle = async () => {
    setError('');
    try {
      const response = await api.get('/puzzle/random');
      setPuzzle(response.data);
    } catch (err) {
      setError(`Ошибка при получении пазла: ${err.response?.data || err.message}`);
    }
  };

  return (
    <div style={{ padding: '20px', fontFamily: 'sans-serif' }}>
      <h1>Игровая панель системы "Данетки"</h1>
      <p style={{ color: 'green' }}>Статус: Авторизован в системе</p>

      <div style={{ margin: '20px 0' }}>
        <button onClick={fetchRandomPuzzle} style={{ padding: '10px', marginRight: '10px', cursor: 'pointer' }}>
          Получить случайную данетку
        </button>
        
        <button onClick={onLogout} style={{ padding: '10px', backgroundColor: '#ff4d4f', color: 'white', border: 'none', cursor: 'pointer' }}>
          Выйти
        </button>
      </div>

      {error && <p style={{ color: 'red' }}>{error}</p>}

      {puzzle && (
        <div style={{ border: '1px solid #ccc', padding: '15px', marginTop: '15px', backgroundColor: '#f9f9f9', maxWidth: '600px' }}>
          <h3>Данные данетки (из базы через gRPC и Gateway):</h3>
          <p><strong>ID:</strong> {puzzle.id}</p>
          <p><strong>Условие:</strong> {puzzle.openPart || puzzle.open_part}</p>
          {puzzle.sourceUrl && <p><strong>Источник:</strong> <a href={puzzle.sourceUrl} target="_blank" rel="noreferrer">{puzzle.sourceUrl}</a></p>}
        </div>
      )}
    </div>
  );
}

export default MainPage;