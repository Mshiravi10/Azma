import { Link } from 'react-router-dom';
import { Compass } from 'lucide-react';
import { Button } from '@/components/ui/button';
import {
  Empty,
  EmptyContent,
  EmptyDescription,
  EmptyHeader,
  EmptyMedia,
  EmptyTitle,
} from '@/components/ui/empty';

export default function NotFoundPage() {
  return (
    <Empty className="min-h-[60vh]">
      <EmptyHeader>
        <EmptyMedia variant="icon">
          <Compass />
        </EmptyMedia>
        <EmptyTitle className="num-fa text-4xl">۴۰۴</EmptyTitle>
        <EmptyDescription>صفحه‌ای که دنبالش بودید پیدا نشد</EmptyDescription>
      </EmptyHeader>
      <EmptyContent>
        <Button asChild size="lg">
          <Link to="/dashboard">بازگشت به داشبورد</Link>
        </Button>
      </EmptyContent>
    </Empty>
  );
}
