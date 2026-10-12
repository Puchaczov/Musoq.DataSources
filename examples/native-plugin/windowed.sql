select Position, PositiveCount(Amount) over
       (partition by Customer order by Position rows between unbounded preceding and current row) as Positive
from invoice.rows('*')
order by Position
