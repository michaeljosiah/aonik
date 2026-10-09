import { useEffect, useState } from 'react';
import { Link, useParams } from 'react-router-dom';
import { Card, PageHeader } from '@/components/layout/aonik';
import { Alert, AlertDescription } from '@/components/ui/alert';
import { Button } from '@/components/ui/button';
import { DataTable, DataTablePagination, type ColumnDef } from '@/components/ui/data-table';
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from '@/components/ui/select';
import { contactEnquiryService, contactTopics, type ContactEnquiryDetail, type ContactEnquirySummary } from '@/services/contactEnquiryService';

export function ContactEnquiriesPage() {
  const [items, setItems] = useState<ContactEnquirySummary[]>([]);
  const [total, setTotal] = useState(0);
  const [page, setPage] = useState(1);
  const [pageSize, setPageSize] = useState(20);
  const [topic, setTopic] = useState('all');
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState(false);
  const [revision, setRevision] = useState(0);
  const reload = () => { setLoading(true); setRevision((value) => value + 1); };

  useEffect(() => {
    let cancelled = false;
    contactEnquiryService.list(page, pageSize, topic === 'all' ? undefined : topic)
      .then((result) => {
        if (cancelled) return;
        setItems(result.items); setTotal(result.totalCount); setError(false); setLoading(false);
      }).catch(() => {
        if (cancelled) return;
        setItems([]); setTotal(0); setError(true); setLoading(false);
      });
    return () => { cancelled = true; };
  }, [page, pageSize, topic, revision]);

  const columns: ColumnDef<ContactEnquirySummary>[] = [
    { id: 'name', header: 'From', cell: (row) => <div><Link className="underline" to={`/contact-enquiries/${row.id}`}>{row.name}</Link><p className="text-sm text-muted-foreground">{row.email}</p></div> },
    { id: 'topic', header: 'Topic', cell: (row) => contactTopics[row.topic] ?? row.topic },
    { id: 'received', header: 'Received', cell: (row) => new Date(row.receivedAtUtc).toLocaleString() },
    { id: 'images', header: 'Photos', accessorKey: 'imageCount' },
  ];
  return <div className="flex flex-col gap-5 p-6 md:px-8">
    <PageHeader title="Contact enquiries" subtitle="Messages received from the storefront" actions={<Button variant="outline" onClick={reload}>Refresh</Button>} />
    <Select value={topic} onValueChange={(value) => { setTopic(value); setPage(1); setLoading(true); }}>
      <SelectTrigger aria-label="Filter by topic" className="w-72"><SelectValue /></SelectTrigger>
      <SelectContent><SelectItem value="all">All topics</SelectItem>{Object.entries(contactTopics).map(([value, label]) => <SelectItem key={value} value={value}>{label}</SelectItem>)}</SelectContent>
    </Select>
    {error && <Alert variant="destructive"><AlertDescription>Enquiries could not be loaded. <Button variant="link" onClick={reload}>Retry</Button></AlertDescription></Alert>}
    <Card padding={0}>{loading ? <p role="status" className="p-6">Loading enquiries…</p> : <>
      <DataTable data={items} columns={columns} getRowId={(row) => row.id} showCheckboxes={false} emptyTitle="No enquiries" emptyDescription="New messages will appear here." />
      <DataTablePagination pageNumber={page} pageSize={pageSize} totalCount={total}
        onPageChange={(value) => { setPage(value); setLoading(true); }}
        onPageSizeChange={(value) => { setPageSize(value); setPage(1); setLoading(true); }} />
    </>}</Card>
  </div>;
}

function ContactPhoto({ enquiryId, image }: { enquiryId: string; image: ContactEnquiryDetail['images'][number] }) {
  const [url, setUrl] = useState<string | null>(null);
  const [failed, setFailed] = useState(false);
  const [revision, setRevision] = useState(0);
  useEffect(() => {
    const controller = new AbortController();
    let objectUrl: string | undefined;
    contactEnquiryService.image(enquiryId, image.id, controller.signal).then((blob) => {
      if (controller.signal.aborted) return;
      objectUrl = URL.createObjectURL(blob); setUrl(objectUrl); setFailed(false);
    }).catch(() => { if (!controller.signal.aborted) setFailed(true); });
    return () => { controller.abort(); if (objectUrl) URL.revokeObjectURL(objectUrl); };
  }, [enquiryId, image.id, revision]);
  return <figure className="space-y-2">
    {url ? <a href={url} download={image.fileName}><img className="max-h-80 max-w-full rounded border object-contain" src={url} alt={image.fileName} /></a>
      : failed ? <Button variant="outline" onClick={() => { setFailed(false); setRevision((value) => value + 1); }}>Retry photo</Button>
        : <p role="status">Loading photo…</p>}
    <figcaption className="text-sm text-muted-foreground">{image.fileName} · {Math.ceil(image.sizeBytes / 1024)} KB</figcaption>
  </figure>;
}

export function ContactEnquiryDetailPage() {
  const { id } = useParams<{ id: string }>();
  const [enquiry, setEnquiry] = useState<ContactEnquiryDetail | null>(null);
  const [error, setError] = useState(false);
  const [revision, setRevision] = useState(0);
  useEffect(() => {
    let cancelled = false;
    if (!id) return;
    contactEnquiryService.get(id).then((result) => {
      if (!cancelled) { setEnquiry(result); setError(false); }
    }).catch(() => { if (!cancelled) { setEnquiry(null); setError(true); } });
    return () => { cancelled = true; };
  }, [id, revision]);
  // Hide the previous enquiry immediately when navigating between detail routes.
  const current = enquiry?.id === id ? enquiry : null;
  return <div className="flex flex-col gap-5 p-6 md:px-8">
    <PageHeader title="Contact enquiry" actions={<Button variant="outline" asChild><Link to="/contact-enquiries">Back to enquiries</Link></Button>} />
    {error ? <Alert variant="destructive"><AlertDescription>This enquiry could not be loaded. <Button variant="link" onClick={() => { setError(false); setRevision((value) => value + 1); }}>Retry</Button></AlertDescription></Alert>
      : !current ? <p role="status">Loading enquiry…</p> : <>
        <Card><dl className="grid gap-4 sm:grid-cols-2">
          <div><dt className="text-sm text-muted-foreground">From</dt><dd>{current.name}<br />{current.email}</dd></div>
          <div><dt className="text-sm text-muted-foreground">Received</dt><dd>{new Date(current.receivedAtUtc).toLocaleString()}</dd></div>
          <div><dt className="text-sm text-muted-foreground">Topic</dt><dd>{contactTopics[current.topic] ?? current.topic}</dd></div>
          {current.orderNumber && <div><dt className="text-sm text-muted-foreground">Order number supplied by customer</dt><dd>{current.orderNumber}</dd></div>}
          <div className="sm:col-span-2"><dt className="text-sm text-muted-foreground">Message</dt><dd className="whitespace-pre-wrap break-words">{current.message}</dd></div>
          <div className="sm:col-span-2"><dt className="text-sm text-muted-foreground">Reference</dt><dd className="break-all font-mono text-sm">{current.id}</dd></div>
        </dl></Card>
        {current.images.length > 0 && <Card><h2 className="mb-4 font-semibold">Photos</h2><div className="grid gap-6 sm:grid-cols-2">{current.images.map((image) => <ContactPhoto key={`${current.id}/${image.id}`} enquiryId={current.id} image={image} />)}</div></Card>}
      </>}
  </div>;
}
