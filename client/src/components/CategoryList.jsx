import React from 'react';

/**
 * @param {{
 *   categories: import('../dataModels').Category[],
 *   onSelectCategory: (categoryName: string | null) => void,
 *   selectedCategory: string | null - the selected category name, or null for "All Recipes"
 * }} props
 */
function CategoryList({ categories, onSelectCategory, selectedCategory }) {
  return (
    <div className="CategoryList">
      <h3 style={{marginTop: '15px', marginBottom: '10px'}}>Categories</h3>
      <ul>
        <li
          key="all"
          onClick={() => onSelectCategory(null)}
          className={selectedCategory === null ? 'active-category' : ''}
        >
          All Recipes
        </li>
        {categories.map(category => (
          <li
            key={category.id}
            onClick={() => onSelectCategory(category.name)}
            className={selectedCategory === category.name ? 'active-category' : ''}
          >
            {category.name}
          </li>
        ))}
      </ul>
    </div>
  );
}

export default CategoryList;
