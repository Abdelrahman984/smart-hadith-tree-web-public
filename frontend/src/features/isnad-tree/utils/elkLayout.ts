import ELK, { type ElkNode, type ElkExtendedEdge } from 'elkjs/lib/elk.bundled.js';
import { Node, Edge } from '@xyflow/react';

const elk = new ELK();

const elkOptions = {
  'elk.algorithm': 'layered',
  'elk.direction': 'DOWN', // Top-to-bottom transmission flow
  'elk.spacing.nodeNode': '85', // Generous horizontal spacing between nodes
  'elk.layered.spacing.nodeNodeBetweenLayers': '135', // Vertical spacing between generational tiers
  'elk.spacing.edgeNode': '35', // Clearance between edges and nodes
  'elk.spacing.edgeEdge': '25', // Separation between edges to avoid clustering
  'elk.layered.spacing.edgeNodeBetweenLayers': '45',
  'elk.layered.spacing.edgeEdgeBetweenLayers': '25',
  'elk.layered.layering.strategy': 'LONGEST_PATH', // Aligns terminal compilers/reference nodes cleanly at bottom
  'elk.layered.nodePlacement.strategy': 'NETWORK_SIMPLEX', // Centers parents over children by minimizing total edge length
  'elk.layered.nodePlacement.favorStraightEdges': 'true', // Keeps vertical teacher-student chains straight
  'elk.layered.crossingMinimization.strategy': 'LAYER_SWEEP', // Minimizes line crossings
  'elk.layered.crossingMinimization.greedySwitchCrossingMinimizer.activationThreshold': '1',
  'elk.layered.thoroughness': '7',
  'elk.alignment': 'CENTER',
  // Route edges around nodes; the routes are rendered by ElkEdge.
  'elk.edgeRouting': 'ORTHOGONAL',
  'elk.layered.unnecessaryBendpoints': 'true',
};

export const getLayoutedElements = async (nodes: Node[], edges: Edge[]) => {
  const graph: ElkNode = {
    id: 'root',
    layoutOptions: elkOptions,
    children: nodes.map((node) => ({
      ...node,
      // Prefer the size React Flow measured; fall back to estimates on the first pass.
      width: node.measured?.width ?? (node.type === 'reference' ? 270 : 250),
      height: node.measured?.height ?? (node.type === 'reference' ? 145 : 135),
    })),
    edges: edges.map((edge) => ({
      id: edge.id,
      sources: [edge.source],
      targets: [edge.target],
    })),
  };

  try {
    const layoutedGraph = await elk.layout(graph);
    const routedEdges = (layoutedGraph.edges ?? []) as ElkExtendedEdge[];

    const layoutedNodes = nodes.map((node) => {
      const layoutedNode = layoutedGraph.children?.find((n) => n.id === node.id);

      return {
        ...node,
        position: {
          x: layoutedNode?.x || 0,
          y: layoutedNode?.y || 0,
        },
      };
    });

    const positionById = new Map(layoutedNodes.map((n) => [n.id, n.position]));

    // Attach ELK's routed polyline to each edge so ElkEdge can draw it.
    const layoutedEdges = edges.map((edge) => {
      const section = routedEdges.find((e) => e.id === edge.id)?.sections?.[0];
      if (!section) return edge;
      return {
        ...edge,
        type: 'elk',
        data: {
          ...edge.data,
          points: [section.startPoint, ...(section.bendPoints ?? []), section.endPoint],
          sourcePos: positionById.get(edge.source),
          targetPos: positionById.get(edge.target),
        },
      };
    });

    return { nodes: layoutedNodes, edges: layoutedEdges };
  } catch (error) {
    console.error("ELK Layout Error:", error);
    return { nodes, edges };
  }
};
