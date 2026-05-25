import React from 'react';
import { Card } from 'antd';

const PuzzleCard = ({ puzzle, loading }) => {

  if (loading) return null;

  if (!puzzle) return null;

  const id = puzzle.puzzleId || puzzle.id || puzzle.PuzzleId || puzzle.puzzle_Id;
  const openPart = puzzle.openPart || puzzle.open_part || puzzle.OpenPart;
  const sourceUrl = puzzle.sourceUrl || puzzle.source_url || puzzle.SourceUrl;

  return (
    <Card 
      type="inner" 
      title={`Данетка № ${id}`} 
      style={{ marginTop: '20px', textAlign: 'left' }}
    >
      {/* Отображаем условие загадки */}
      <p style={{ fontSize: '18px', lineHeight: '1.6', color: '#333' }}>
        {openPart}
      </p>

      {/* Если есть ссылка на источник, аккуратно её выводим */}
      {sourceUrl && (
        <div style={{ marginTop: '15px', textAlign: 'right' }}>
          <a href={sourceUrl} target="_blank" rel="noreferrer" style={{ fontSize: '14px' }}>
            Источник материала →
          </a>
        </div>
      )}
    </Card>
  );
};

export default PuzzleCard;