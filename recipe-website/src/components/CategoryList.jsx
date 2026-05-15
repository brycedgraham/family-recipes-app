import React from 'react';

/**
 * @param {{
 *   categories: import('../dataModels').Category[],
 *   onSelectCategory: (categoryId: string | null) => void,
 *   selectedCategoryId: string | null
 * }} props
 */
function CategoryList({ categories, onSelectCategory, selectedCategoryId }) {
  return (
    <div className="CategoryList">
      <h3 style={{marginTop: '15px', marginBottom: '10px'}}>Categories</h3>
      <ul>
        <li
          key="all"
          onClick={() => onSelectCategory(null)}
          className={selectedCategoryId === null ? 'active-category' : ''}
        >
          All Recipes
        </li>
        {categories.map(category => (
          <li
            key={category.id}
            onClick={() => onSelectCategory(category.id)}
            className={selectedCategoryId === category.id ? 'active-category' : ''}
          >
            {category.name}
          </li>
        ))}
      </ul>
    </div>
  );
}

export default CategoryList;
